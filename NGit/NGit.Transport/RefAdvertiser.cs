using System.Collections.Generic;
using System.IO;
using System.Text;
using NGit;
using NGit.Revwalk;
using NGit.Transport;
using NGit.Util;
using Sharpen;

namespace NGit.Transport
{
	/// <summary>
	/// Support for the start of
	/// <see cref="UploadPack">UploadPack</see>
	/// and
	/// <see cref="ReceivePack">ReceivePack</see>
	/// .
	/// </summary>
	public abstract class RefAdvertiser
	{
		/// <summary>
		/// Advertiser which frames lines in a
		/// <see cref="PacketLineOut">PacketLineOut</see>
		/// format.
		/// </summary>
		public class PacketLineOutRefAdvertiser : RefAdvertiser
		{
			private readonly PacketLineOut pckOut;

			/// <summary>Create a new advertiser for the supplied stream.</summary>
			/// <remarks>Create a new advertiser for the supplied stream.</remarks>
			/// <param name="out">the output stream.</param>
			public PacketLineOutRefAdvertiser(PacketLineOut @out)
			{
				pckOut = @out;
			}

			/// <exception cref="System.IO.IOException"></exception>
			protected internal override void WriteOne(CharSequence line)
			{
				pckOut.WriteString(line.ToString());
			}

			/// <exception cref="System.IO.IOException"></exception>
			protected internal override void End()
			{
				pckOut.End();
			}
		}

		private RevWalk walk;

		private RevFlag ADVERTISED;

		private readonly StringBuilder tmpLine = new StringBuilder(100);

		private readonly char[] tmpId = new char[Constants.OBJECT_ID_STRING_LENGTH];

		private readonly ICollection<string> capablities = new LinkedHashSet<string>();

		private bool derefTags;

		private bool first = true;

		/// <summary>Initialize a new advertisement formatter.</summary>
		/// <remarks>Initialize a new advertisement formatter.</remarks>
		/// <param name="protoWalk">the RevWalk used to parse objects that are advertised.</param>
		/// <param name="advertisedFlag">
		/// flag marked on any advertised objects parsed out of the
		/// <code>protoWalk</code>
		/// 's object pool, permitting the caller to
		/// later quickly determine if an object was advertised (or not).
		/// </param>
		public virtual void Init(RevWalk protoWalk, RevFlag advertisedFlag)
		{
			walk = protoWalk;
			ADVERTISED = advertisedFlag;
		}

		/// <summary>Toggle tag peeling.</summary>
		/// <remarks>
		/// Toggle tag peeling.
		/// <p>
		/// <p>
		/// This method must be invoked prior to any of the following:
		/// <ul>
		/// <li>
		/// <see cref="Send(System.Collections.Generic.IDictionary{K, V})">Send(System.Collections.Generic.IDictionary&lt;K, V&gt;)
		/// 	</see>
		/// <li>
		/// <see cref="AdvertiseHave(NGit.AnyObjectId)">AdvertiseHave(NGit.AnyObjectId)</see>
		/// <li>
		/// <see cref="IncludeAdditionalHaves(NGit.Repository)">IncludeAdditionalHaves(NGit.Repository)
		/// 	</see>
		/// </ul>
		/// </remarks>
		/// <param name="deref">
		/// true to show the dereferenced value of a tag as the special
		/// ref <code>$tag^{}</code> ; false to omit it from the output.
		/// </param>
		public virtual void SetDerefTags(bool deref)
		{
			derefTags = deref;
		}

		/// <summary>Add one protocol capability to the initial advertisement.</summary>
		/// <remarks>
		/// Add one protocol capability to the initial advertisement.
		/// <p>
		/// This method must be invoked prior to any of the following:
		/// <ul>
		/// <li>
		/// <see cref="Send(System.Collections.Generic.IDictionary{K, V})">Send(System.Collections.Generic.IDictionary&lt;K, V&gt;)
		/// 	</see>
		/// <li>
		/// <see cref="AdvertiseHave(NGit.AnyObjectId)">AdvertiseHave(NGit.AnyObjectId)</see>
		/// <li>
		/// <see cref="IncludeAdditionalHaves(NGit.Repository)">IncludeAdditionalHaves(NGit.Repository)
		/// 	</see>
		/// </ul>
		/// </remarks>
		/// <param name="name">
		/// the name of a single protocol capability supported by the
		/// caller. The set of capabilities are sent to the client in the
		/// advertisement, allowing the client to later selectively enable
		/// features it recognizes.
		/// </param>
		public virtual void AdvertiseCapability(string name)
		{
			capablities.AddItem(name);
		}

		/// <summary>Format an advertisement for the supplied refs.</summary>
		/// <remarks>Format an advertisement for the supplied refs.</remarks>
		/// <param name="refs">
		/// zero or more refs to format for the client. The collection is
		/// sorted before display if necessary, and therefore may appear
		/// in any order.
		/// </param>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		public virtual void Send(IDictionary<string, Ref> refs)
		{
			foreach (Ref r in GetSortedRefs(refs))
			{
				RevObject obj = ParseAnyOrNull(r.GetObjectId());
				if (obj != null)
				{
					AdvertiseAny(obj, r.GetName());
					if (derefTags && obj is RevTag)
					{
						AdvertiseTag((RevTag)obj, r.GetName() + "^{}");
					}
				}
			}
		}

		private Iterable<Ref> GetSortedRefs(IDictionary<string, Ref> all)
		{
			if (all is RefMap || (all is SortedDictionary<string,Ref>))
			{
				return all.Values.AsIterable ();
			}
			return RefComparator.Sort(all.Values).AsIterable ();
		}

		/// <summary>
		/// Advertise one object is available using the magic
		/// <code>.have</code>
		/// .
		/// <p>
		/// The magic
		/// <code>.have</code>
		/// advertisement is not available for fetching by a
		/// client, but can be used by a client when considering a delta base
		/// candidate before transferring data in a push. Within the record created
		/// by this method the ref name is simply the invalid string
		/// <code>.have</code>
		/// .
		/// </summary>
		/// <param name="id">identity of the object that is assumed to exist.</param>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		public virtual void AdvertiseHave(AnyObjectId id)
		{
			RevObject obj = ParseAnyOrNull(id);
			if (obj != null)
			{
				AdvertiseAnyOnce(obj, ".have");
				if (obj is RevTag)
				{
					AdvertiseAnyOnce(((RevTag)obj).GetObject(), ".have");
				}
			}
		}

		/// <summary>
		/// Include references of alternate repositories as
		/// <code>.have</code>
		/// lines.
		/// </summary>
		/// <param name="src">repository to get the additional reachable objects from.</param>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		public virtual void IncludeAdditionalHaves(Repository src)
		{
			foreach (ObjectId id in src.GetAdditionalHaves())
			{
				AdvertiseHave(id);
			}
		}

		/// <returns>true if no advertisements have been sent yet.</returns>
		public virtual bool IsEmpty()
		{
			return first;
		}

		private RevObject ParseAnyOrNull(AnyObjectId id)
		{
			if (id == null)
			{
				return null;
			}
			try
			{
				return walk.ParseAny(id);
			}
			catch (IOException)
			{
				return null;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void AdvertiseAnyOnce(RevObject obj, string refName)
		{
			if (!obj.Has(ADVERTISED))
			{
				AdvertiseAny(obj, refName);
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void AdvertiseAny(RevObject obj, string refName)
		{
			obj.Add(ADVERTISED);
			AdvertiseId(obj, refName);
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void AdvertiseTag(RevTag tag, string refName)
		{
			RevObject o = tag;
			do
			{
				// Fully unwrap here so later on we have these already parsed.
				RevObject target = ((RevTag)o).GetObject();
				try
				{
					walk.ParseHeaders(target);
				}
				catch (IOException)
				{
					return;
				}
				target.Add(ADVERTISED);
				o = target;
			}
			while (o is RevTag);
			AdvertiseAny(tag.GetObject(), refName);
		}

		/// <summary>Advertise one object under a specific name.</summary>
		/// <remarks>
		/// Advertise one object under a specific name.
		/// <p>
		/// If the advertised object is a tag, this method does not advertise the
		/// peeled version of it.
		/// </remarks>
		/// <param name="id">the object to advertise.</param>
		/// <param name="refName">
		/// name of the reference to advertise the object as, can be any
		/// string not including the NUL byte.
		/// </param>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		public virtual void AdvertiseId(AnyObjectId id, string refName)
		{
			tmpLine.Length = 0;
			id.CopyTo(tmpId, tmpLine);
			tmpLine.Append(' ');
			tmpLine.Append(refName);
			if (first)
			{
				first = false;
				if (!capablities.IsEmpty())
				{
					tmpLine.Append('\0');
					foreach (string capName in capablities)
					{
						tmpLine.Append(' ');
						tmpLine.Append(capName);
					}
					tmpLine.Append(' ');
				}
			}
			tmpLine.Append('\n');
			WriteOne(tmpLine);
		}

		/// <summary>Write a single advertisement line.</summary>
		/// <remarks>Write a single advertisement line.</remarks>
		/// <param name="line">
		/// the advertisement line to be written. The line always ends
		/// with LF. Never null or the empty string.
		/// </param>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		protected internal abstract void WriteOne(CharSequence line);

		/// <summary>Mark the end of the advertisements.</summary>
		/// <remarks>Mark the end of the advertisements.</remarks>
		/// <exception cref="System.IO.IOException">
		/// the underlying output stream failed to write out an
		/// advertisement record.
		/// </exception>
		protected internal abstract void End();
	}
}