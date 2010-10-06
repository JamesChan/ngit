using System;
using System.Collections.Generic;
using NGit;
using NGit.Revplot;
using NGit.Revwalk;
using Sharpen;

namespace NGit.Revplot
{
	/// <summary>
	/// An ordered list of
	/// <see cref="PlotCommit{L}">PlotCommit&lt;L&gt;</see>
	/// subclasses.
	/// <p>
	/// Commits are allocated into lanes as they enter the list, based upon their
	/// connections between descendant (child) commits and ancestor (parent) commits.
	/// <p>
	/// The source of the list must be a
	/// <see cref="PlotWalk">PlotWalk</see>
	/// and
	/// <see cref="NGit.Revwalk.RevCommitList{E}.FillTo(int)">NGit.Revwalk.RevCommitList&lt;E&gt;.FillTo(int)
	/// 	</see>
	/// must be used to populate the list.
	/// </summary>
	/// <?></?>
	public class PlotCommitList<L> : RevCommitList<PlotCommit<L>> where L:PlotLane
	{
		internal const int MAX_LENGTH = 25;

		private int positionsAllocated;

		private readonly TreeSet<int> freePositions = new TreeSet<int>();

		private readonly HashSet<PlotLane> activeLanes = new HashSet<PlotLane>();

		public override void Clear()
		{
			base.Clear();
			positionsAllocated = 0;
			freePositions.Clear();
			activeLanes.Clear();
		}

		public override void Source(RevWalk w)
		{
			if (!(w is PlotWalk))
			{
				throw new InvalidCastException(MessageFormat.Format(JGitText.Get().classCastNotA, 
					typeof(PlotWalk).FullName));
			}
			base.Source(w);
		}

		/// <summary>Find the set of lanes passing through a commit's row.</summary>
		/// <remarks>
		/// Find the set of lanes passing through a commit's row.
		/// <p>
		/// Lanes passing through a commit are lanes that the commit is not directly
		/// on, but that need to travel through this commit to connect a descendant
		/// (child) commit to an ancestor (parent) commit. Typically these lanes will
		/// be drawn as lines in the passed commit's box, and the passed commit won't
		/// appear to be connected to those lines.
		/// <p>
		/// This method modifies the passed collection by adding the lanes in any
		/// order.
		/// </remarks>
		/// <param name="currCommit">the commit the caller needs to get the lanes from.</param>
		/// <param name="result">collection to add the passing lanes into.</param>
		public virtual void FindPassingThrough(PlotCommit<L> currCommit, ICollection<L> result
			)
		{
			foreach (PlotLane p in currCommit.passingLanes)
			{
				result.AddItem((L)p);
			}
		}

		protected internal override void Enter(int index, PlotCommit<L> currCommit)
		{
			SetupChildren(currCommit);
			int nChildren = currCommit.GetChildCount();
			if (nChildren == 0)
			{
				return;
			}
			if (nChildren == 1 && currCommit.children[0].ParentCount < 2)
			{
				// Only one child, child has only us as their parent.
				// Stay in the same lane as the child.
				//
				PlotCommit c = currCommit.children[0];
				if (c.lane == null)
				{
					// Hmmph. This child must be the first along this lane.
					//
					c.lane = NextFreeLane();
					activeLanes.AddItem(c.lane);
				}
				for (int r = index - 1; r >= 0; r--)
				{
					PlotCommit rObj = this[r];
					if (rObj == c)
					{
						break;
					}
					rObj.AddPassingLane(c.lane);
				}
				currCommit.lane = c.lane;
			}
			else
			{
				// More than one child, or our child is a merge.
				// Use a different lane.
				//
				// Process all our children. Especially important when there is more
				// than one child (e.g. a commit is processed where other branches
				// fork out). For each child the following is done
				// 1. If no lane was assigned to the child a new lane is created and
				// assigned
				// 2. The lane of the child is closed. If this frees a position,
				// this position will be added freePositions list.
				// If we have multiple children which where previously not on a lane
				// each such child will get his own new lane but all those new lanes
				// will be on the same position. We have to take care that not
				// multiple newly created (in step 1) lanes occupy that position on
				// which the
				// parent's lane will be on. Therefore we delay closing the lane
				// with the parents position until all children are processed.
				// The lane on that position the current commit will be on
				PlotLane reservedLane = null;
				for (int i = 0; i < nChildren; i++)
				{
					PlotCommit c = currCommit.children[i];
					// don't forget to position all of your children if they are
					// not already positioned.
					if (c.lane == null)
					{
						c.lane = NextFreeLane();
						activeLanes.AddItem(c.lane);
						if (reservedLane != null)
						{
							CloseLane(c.lane);
						}
						else
						{
							reservedLane = c.lane;
						}
					}
					else
					{
						if (reservedLane == null && activeLanes.Contains(c.lane))
						{
							reservedLane = c.lane;
						}
						else
						{
							CloseLane(c.lane);
						}
					}
				}
				// finally all children are processed. We can close the lane on that
				// position our current commit will be on.
				if (reservedLane != null)
				{
					CloseLane(reservedLane);
				}
				currCommit.lane = NextFreeLane();
				activeLanes.AddItem(currCommit.lane);
				// take care: when connecting yourself to your child make sure that
				// you will not be located on a lane on which a passed commit is
				// located on. Otherwise we would have to draw a line through a
				// commit.
				int remaining = nChildren;
				BitSet blockedPositions = new BitSet();
				for (int r = index - 1; r >= 0; r--)
				{
					PlotCommit rObj = this[r];
					if (currCommit.IsChild(rObj))
					{
						if (--remaining == 0)
						{
							break;
						}
					}
					if (rObj != null)
					{
						PlotLane lane = rObj.GetLane();
						if (lane != null)
						{
							blockedPositions.Set(lane.GetPosition());
						}
						rObj.AddPassingLane(currCommit.lane);
					}
				}
				// Now let's check whether we have to reposition the lane
				if (blockedPositions.Get(currCommit.lane.GetPosition()))
				{
					int newPos = -1;
					foreach (int pos in freePositions)
					{
						if (!blockedPositions.Get(pos))
						{
							newPos = pos;
							break;
						}
					}
					if (newPos == -1)
					{
						newPos = positionsAllocated++;
					}
					freePositions.AddItem(currCommit.lane.GetPosition());
					currCommit.lane.position = newPos;
				}
			}
		}

		private void CloseLane(PlotLane lane)
		{
			RecycleLane((L)lane);
			if (activeLanes.Remove(lane))
			{
				freePositions.AddItem(Sharpen.Extensions.ValueOf(lane.GetPosition()));
			}
		}

		private void SetupChildren(PlotCommit<L> currCommit)
		{
			int nParents = currCommit.ParentCount;
			for (int i = 0; i < nParents; i++)
			{
				((PlotCommit)currCommit.GetParent(i)).AddChild(currCommit);
			}
		}

		private PlotLane NextFreeLane()
		{
			PlotLane p = CreateLane();
			if (freePositions.IsEmpty())
			{
				p.position = positionsAllocated++;
			}
			else
			{
				int min = freePositions.First();
				p.position = min;
				freePositions.Remove(min);
			}
			return p;
		}

		/// <returns>a new Lane appropriate for this particular PlotList.</returns>
		protected internal virtual L CreateLane()
		{
			return (L)new PlotLane();
		}

		/// <summary>
		/// Return colors and other reusable information to the plotter when a lane
		/// is no longer needed.
		/// </summary>
		/// <remarks>
		/// Return colors and other reusable information to the plotter when a lane
		/// is no longer needed.
		/// </remarks>
		/// <param name="lane"></param>
		protected internal virtual void RecycleLane(L lane)
		{
		}
		// Nothing.
	}
}