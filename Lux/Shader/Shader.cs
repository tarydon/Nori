// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Shader.cs
// ║║║║╬║╔╣║ Temporary code - preparing for Shader<T, U>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;

namespace Nori;

#region class Shader -------------------------------------------------------------------------------
/// <summary>The internal base class for all Shader(T,U)</summary>
/// This exists mainly so we can have a non-generic base class for all the Shader(T,U) parametrized
/// types. Having a non generic base class allows us to create a collection of all such Shader
/// objects.
abstract class Shader {
   // Constructor --------------------------------------------------------------
   /// <summary>Construct a ShaderImp given the underlying ShaderImp</summary>
   protected Shader (ShaderImp program) {
      CBVertex = Attrib.GetSize ((Pgm = program).VSpec);
      Attribs = Attrib.GetFor (program.VSpec);
      SortCode = Pgm.SortCode;
      Idx = (ushort)mAll.Count; 
      mAll.Add (this);
   }
   public readonly Attrib[] Attribs;

   // Properties --------------------l-------------------------------------------
   /// <summary>Returns the size of each vertex (the sum of sizes of the Attrib array)</summary>
   public readonly int CBVertex;

   /// <summary>The index of this Shader (used for RBatch.NShader)</summary>
   public readonly ushort Idx;

   /// <summary>The underlying shader program this wraps around</summary>
   public readonly ShaderImp Pgm;

   /// <summary>The sort-code for this program</summary>
   public readonly int SortCode;

   // Methods ------------------------------------------------------------------
   /// <summary>Gets a shader, given its index</summary>
   public static Shader Get (int index) => mAll[index];

   /// <summary>This is called for every shader at the start of every frame</summary>
   public static void StartFrame () {
      mAll.ForEach (a => a?.Cleanup ());
      mApplyUniforms = 0;
   }
   protected internal static int mApplyUniforms;

   // Overrrideables -----------------------------------------------------------
   /// <summary>Override this to apply a particular UBlock into the shader program</summary>
   /// During the rendering cycle (for each frame), we capture uniforms into the uniform array
   /// that each shader maintains:
   ///    List(UBlock) mUniforms = [];
   /// Finally, after all the data is captured, during rendering, we 'apply' one of these sets
   /// of uniforms by calling ApplyUniforms(int). That actually applies the data from the typed
   /// UBlock into the shader by calling one of the GL.SetUniform() variants
   public abstract void ApplyUniforms (int idxUniform);

   /// <summary>Copy vertex data to the specified RBuffer</summary>
   /// When we create the draw calls (using Lux.Lines, Lux.Mesh etc), the data is first
   /// gathered in the respective mData buffers of each shader. Then later when the entire
   /// scene has been thus drawn, we move all this data into RBuffer objects using this method.
   public abstract int CopyVertices (RetainBuffer buffer, int start, int count);

   /// <summary>Copy indexed vertex data to the specified RBuffer</summary>
   /// This is similar to the routine above, but the drawing in this case is done using indices.
   /// That is, if we want to draw N vertices, we don't actually submit N vertices, but submit only
   /// M (where M .lt. N) and then use a separate _index_ array (that contains only integers). This index
   /// array contains N elements, which act as indices into the vertex array. For example, if we are
   /// drawing a square using 4 vertices, we might pass in a vertex array like this:
   ///    [(0, 0, 0), (100, 0, 0), (100, 100, 0), (100, 100, 0)]
   /// Then, that could be drawn using 2 triangle calls using these above vertices (numbered 0..3)
   /// thus:
   ///   [0, 1, 2,   0, 2, 3]
   /// Each set of 3 integers, treated as indices into the array above, provides the vertices for
   /// a triangle, which together make up the square. The point of this indirection is that we
   /// don't have to actually pass in 6 'Point3' vertices (which would cost us more memory). Instead,
   /// we pass in just the 4 unique vertices and reuse them by specifying some indices (like 0, 2)
   /// more than once. In OpenGL terminology, this is the difference between the simpler glDrawArrays,
   /// and the more complex glDrawElements (this CopyVertices maps to a glDrawElements call, the
   /// earlier one to a glDrawArrays call).
   public abstract (int, int) CopyVertices (RetainBuffer buffer, int start, int count, int istart, int icount);

   /// <summary>This is called after each frame to cleanup any frame-specific artifacts / data</summary>
   /// For all shaders, we clear the mUniforms[] array
   /// For all streaming shaders, we clear the mData array (stores vertex data)
   public abstract void Cleanup ();

   /// <summary>Provides a human-readable description of a set of uniforms</summary>
   public abstract string DescribeUniforms (int n);

   /// <summary>This is used to order two UBlock objects, given their indices</summary>
   /// This is used to sort batches by order of issuance (for example, we want to issue
   /// batches by increasing ZLevel). Also, if this compare returns 0, it means that the
   /// unforms of two batches are exactly the same, and these batches could be merged.
   public abstract int OrderUniforms (int id1, int id2);

   /// <summary>Override this to set the 'constant uniforms' that don't vary at all during the entire frame</summary>
   /// Typically these are uniforms like viewport size, that never change during a frame,
   /// and they are called just once when we start rendering the entire issue of batches
   public abstract void SetConstants ();

   /// <summary>This is called to 'capture' the current uniforms into a UBlock structure</summary>
   /// This is overridden in each of the shaders to capture the relevant globals
   /// like Lux.DrawColor, Lux.LineWidth into the UBlock of that shader. Since each
   /// shader uses a different set of uniforms, this has to be a virtual function.
   /// This returns the index of the UBlock with that shader's Uniforms list. Later,
   /// that can be applied into a shader program by calling ApplyUniforms(int).
   public abstract ushort SnapUniforms ();

   public abstract void StreamBatches (List<int> ids);

   // Private data -------------------------------------------------------------
   // The list of all shaders - Shader.Idx indexes into this list
   protected static List<Shader> mAll = [null!];
}
#endregion

#region class Shader<Vertex, UBlock> ---------------------------------------------------------------
/// <summary>The Shader(TVertex,TUniform) class 'manages' batched calls to a particular shader</summary>
/// <typeparam name="TVertex">The type of vertex data for this (this is the sum of all 'in' parameters to the vertex shader)</typeparam>
/// <typeparam name="TUniform">The set of uniforms for this (the sum of all 'uniform xxx' declarations for all the stages in the pipeline)</typeparam>
///
/// When a Shader is created, it first 'binds' to the shader by enumerating all the uniforms and
/// getting their addresses. These are stored in the fields with names like muVPScale (where VPScale is
/// the internal name of the uniform). Thus, for each uniform ABC that the shader has, we need to have a
/// corresponding muABC field in the Shader.
///
/// Then, when Draw calls are made, this Shader gathers the actual vertex data into the
/// mData list, and the corresponding batch calls (that provide all the uniforms for that batch)
/// into the RBatch.All heap. If the uniforms have not changed, we keep 'extending' the previous batch,
/// rather than incrementally create small batches. The idea of batching, therefore, is to create several
/// larger batches (with the same uniforms) rather than multiple individual batches. Then, during
/// dispatch, each larger batch is issued with a single DrawElements call.
///
/// The first level of this batching is when these draw calls are made with the same Uniforms as the
/// last time - the previous batch keeps getting extended. A further level of optimization happens
/// in RBatch.IssueAll() - that further 'sorts' all the available batches we have, and
/// chunks together successive batches that have the same uniforms before issuing. See RBatch.Sort
/// for more details on this
///
/// Even within the uniforms, we try to arrange the sorting by most expensive uniform first (thus, we
/// sort first by things like Mat4F, then by things like Vec4F and finally by float uniforms (descending
/// order of cost).
abstract class Shader<TVertex, TUniform> : Shader, IComparer<TUniform> where TVertex : unmanaged {
   // Constructor --------------------------------------------------------------
   protected Shader (ShaderImp shader) : base (shader) { }

   // Methods ------------------------------------------------------------------
   /// <summary>Adds some vertices into our local data array, and creates an RBatch pointing to them</summary>
   public void Draw (ReadOnlySpan<TVertex> data) {
      if (data.Length == 0) return;
      ushort nUniform = SnapUniforms ();
      VNode vnode = Lux.VNode!;
      if (!ExtendBatch (data.Length)) {
         ref RBatch rb = ref RBatch.Alloc ();
         rb.IDVNode = (ushort)vnode.Id;
         rb.Streaming = vnode.Streaming;
         rb.ZLevel = (short)Lux.ZLevel;
         rb.NShader = Idx; rb.NUniform = nUniform; rb.NBuffer = 0;
         rb.Offset = mData.Count; rb.Count = data.Length;
         if (vnode.Streaming) 
            RBatch.Staging.Add ((rb.Idx, rb.NUniform));
         else 
            vnode.Batches.Add ((rb.Idx, rb.NUniform));
      }
      mData.AddRange (data);

      // Helper fuction to see if we can just extend the last batch we added to
      // include these vertices as well. For this to work:
      // - The previous batch should be for the same VNode (since we are going to store
      //   this batch in the mBatch list of that VNode).
      // - The previous batch should use the same shader (this)
      // - The previous batch should be using the same set of uniforms
      bool ExtendBatch (int delta) {
         ref RBatch rb = ref RBatch.Recent ();
         if (rb.Streaming) return false;
         if (rb.IDVNode != vnode.Id) return false;
         if (rb.NShader != Idx || rb.NUniform != nUniform || rb.NBuffer != 0) return false;
         rb.Extend (delta);
         return true;
      }
   }

   /// <summary>Adds vertices and element indices into our local data array, and creates an RBatch pointing to them</summary>
   /// Since we have vertices and indices, we are going to later use this for a DrawElements call,
   /// while the version of Draw above results in a RBatch that uses no 'indices' and is a simple
   /// DrawArrays call. How do we distinguish between the two types of RBatch? This indexed-drawing
   /// RBatch has a non-zero ICount value.
   public void Draw (ReadOnlySpan<TVertex> data, ReadOnlySpan<int> indices) {
      ref RBatch rb = ref RBatch.Alloc ();
      VNode vnode = Lux.VNode!;
      rb.IDVNode = (ushort)vnode.Id;
      rb.ZLevel = (short)Lux.ZLevel;
      rb.NShader = Idx; rb.NUniform = SnapUniforms (); rb.NBuffer = 0;
      rb.Offset = mData.Count; rb.Count = data.Length;
      rb.IOffset = mIndex.Count; rb.ICount = indices.Length;
      if (vnode.Streaming) 
         RBatch.Staging.Add ((rb.Idx, rb.NUniform));
      else 
         vnode.Batches.Add ((rb.Idx, rb.NUniform));

      mData.AddRange (data);
      // Note that these indices are all zero-relative (as in the original mesh data). Later, when
      // we copy these indices into an RBuffer's index data, they continue to remain zero relative.
      // However, the actual position of the vertex data in the final RBuffer is not starting at zero,
      // so we have to use DrawElementsBaseVertex and pass the starting index of this batch's vertex
      // data to that
      mIndex.AddRange (indices);
   }

   public unsafe override void StreamBatches (List<int> ids) {
      // Select this program for use
      GLState.Program = Pgm;
      // Set the shader 'constants' - this is stuff like VPScale that does
      // not change during the frame rendering, and this actually does some
      // setting only once per frame, per shader
      SetConstants ();
      // Apply the uniforms for this set of batches. Note that this is called from 
      // IssueAll which already has ensured that the batches specified in ids all use the same
      // set of uniforms
      ref RBatch rb0 = ref RBatch.Get (ids[0]);
      ApplyUniforms (rb0.NUniform);

      var span = mData.AsSpan ();
      int cbStruct = Marshal.SizeOf<TVertex> (), nSortedUsed = 0;
      mSorted ??= new byte[64];
      fixed (void* p0 = &span[0]) {
         byte* pSrc = (byte*)p0;
         foreach (var id in ids) {
            ref RBatch rb = ref RBatch.Get (id);
            int cbBatch = rb.Count * cbStruct;     // Size of this batch's data, in bytes
            while (nSortedUsed + cbBatch >= mSorted.Length)
               Array.Resize (ref mSorted, mSorted.Length * 2);
            fixed (byte* pDst= &mSorted[0]) 
               Buffer.MemoryCopy (pSrc + rb.Offset * cbStruct, pDst + nSortedUsed, cbBatch, cbBatch);
            nSortedUsed += cbBatch;
         }
      }
      fixed (void* pSorted = &mSorted[0])
         StreamBuffer.It.Draw (Pgm, pSorted, nSortedUsed / cbStruct, Attribs);
   }
   byte[]? mSorted;

   // Overrides ----------------------------------------------------------------
   /// <summary>Copies vertices from our local mData storage to an RBuffer</summary>
   /// This copies 'count' vertices from our local mData storage into the given
   /// RBuffer. This means effectively 'count * CBVertex' bytes of data, This returns
   /// the byte offset within the RBuffer where the data has been copied.
   public override unsafe int CopyVertices (RetainBuffer buffer, int offset, int count) {
      var span = CollectionsMarshal.AsSpan (mData);
      fixed (void* p = &span[offset])
         return buffer.AddData (p, count * CBVertex);
   }

   /// <summary>Variant that copies not only vertices but also indices</summary>
   /// The vertices are copied from our local mData storage into the given RBuffer (this will
   /// copy 'count * CBVertex' bytes of data. Indices are copied from our local mIndex array
   /// into the RBuffer's private index array (this will copy '4 * icount' bytes of data,
   /// since the indices are always integers). This returns a tuple: (dataOffset, indexOffset)
   /// where dataOffset is the _byte_ offset within the RBuffer where the vertex data has been
   /// copied. And indexOffset is the index (not byte-offset) into the RBuffer's index buffer
   /// where the indices have been copied. Both of these are used later as arguments for
   /// a DrawElementsBaseVertex call.
   public override (int, int) CopyVertices (RetainBuffer buffer, int offset, int count, int ioffset, int icount) {
      int dataOffset = CopyVertices (buffer, offset, count);
      var span = CollectionsMarshal.AsSpan (mIndex);
      int indexOffset = buffer.AddIndices (span[ioffset..(ioffset + icount)]);
      return (dataOffset, indexOffset);
   }

   /// <summary>Describe a particular uniform in human readable way (used for debugging)</summary>
   /// This description forms part of an RBatch.ToString() description
   public override string DescribeUniforms (int id)
      => mUniforms[id]?.ToString () ?? "";

   /// <summary>Returns the Nth set of uniforms</summary>
   public TUniform GetUniforms (int nUniform) => mUniforms[nUniform];

   /// <summary>Override this to compare the 'uniform data' of two batches</summary>
   /// This must provide a definitive ordering, to ensure that all batches with similar
   /// uniforms get grouped together so that we reduce the number of issues
   /// TODO: Make this use ref UBlock?
   protected abstract int OrderUniformsImp (ref readonly TUniform a, ref readonly TUniform b);

   public override int OrderUniforms (int id1, int id2) {
      if (id1 == id2) return 0;
      var span = mUniforms.AsSpan ();
      ref readonly TUniform ub1 = ref span[id1], ub2 = ref span[id2];
      return OrderUniformsImp (in ub1, in ub2);
   }

   /// <summary>Override this to set up the uniforms for this batch</summary>
   /// TODO: Make this use ref UBlock?
   protected abstract void ApplyUniformsImp (ref readonly TUniform settings);

   public override void ApplyUniforms (int nUniform) {
      var span = mUniforms.AsSpan ();
      ref readonly TUniform ub = ref span[nUniform];
      ApplyUniformsImp (in ub);
      mApplyUniforms++;
   }

   /// <summary>Helper used by SnapUniforms to do the actual encapsulation of uniforms into a UBlock object</summary>
   protected abstract TUniform SnapUniformsImp ();

   /// <summary>Helper used by SetConstants to do the actual setting of constants</summary>
   protected abstract void SetConstantsImp ();

   // Implementation -----------------------------------------------------------
   // Called internally to bind internal uniform-address fields like muVPScale, muDrawColor etc to
   // the corresponding uniform IDs - these are then used in functions like SetConstants and SetUniforms
   protected void Bind () {
      Type? type = GetType ();
      List<FieldInfo> fields = [];
      while (type != null) {
         fields.AddRange (type.GetFields (BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
         type = type.BaseType;
      }
      foreach (var f in fields)
         if (f.Name.StartsWith ("mu") && f.FieldType.FullName == "System.Int32") {
            int id = Pgm.GetUniformId (f.Name[2..]);
            if (id == -1) Debug.WriteLine ($"Uniform '{f.Name[2..]}' not found in shader '{Pgm.Name}'");
            f.SetValue (this, id);
         }
   }

   /// <summary>Called at the end of every frame</summary>
   public override void Cleanup () { mUniforms.Clear (); mData.Clear (); mIndex.Clear (); }

   /// <summary>Implementation of IComparer interface</summary>
   public int Compare (TUniform? a, TUniform? b) => OrderUniformsImp (in a!, in b!);

   /// <summary>Set the constants (like viewport size) that don't change during the entire frame</summary>
   public sealed override void SetConstants () {
      // If this shader has already been used in this frame (mRung2 == Lux.Rung),
      // then the constants have already been set, and we don't need ot set them again
      if (!Lib.Set (ref mRung2, Lux.Rung)) return;
      SetConstantsImp ();
   }
   int mRung2;

   /// <summary>Captures the current uniforms into mUniforms and returns that index</summary>
   /// We try to reuse the last-used uniforms as far as possible
   public sealed override ushort SnapUniforms () {
      // If the uniforms have not changed at all since the last time we called SnapUniforms,
      // just reuse the last one. Note that this comparison will never return true when
      // mUniforms is empty because we bump up Lux.Rung at the start of each frame, and our
      // own internal mRung value will never match for the first time this shader is used
      // in that frame.
      if (!Lib.Set (ref mRung1, Lux.Rung)) return (ushort)(mUniforms.Count - 1);      // Fast happy path

      // Otherwise, we capture a new set of uniforms (from the Lux state like Lux.DrawColor,
      // Lux.BorderColor etc). That could also actually end up equivalent to the last used
      // uniforms, so we recycle that if OrderUniforms returns 0
      int n = mUniforms.Count;
      mUniforms.Add (SnapUniformsImp ());    // New uniform added at index n
      if (n == 0 || OrderUniforms (n - 1, n) != 0) return (ushort)n;
      mUniforms.RemoveAt (n);
      return (ushort)(n - 1);
   }
   int mRung1;

   // Private data -------------------------------------------------------------
   readonly List<TUniform> mUniforms = [];
   readonly List<TVertex> mData = [];
   readonly List<int> mIndex = [];
}
#endregion
