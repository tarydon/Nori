// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ RBuffer.cs
// ║║║║╬║╔╣║ Implements the 'retained buffer' flavor of VAO (Vertex Array Object)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Ptr = nint;

#region class RBuffer ------------------------------------------------------------------------------
/// <summary>A wrapper around a VertexArrayObject (VAO), used for 'retained mode' drawing</summary>
/// We can store vertex data in a RBuffer, if we intend to keep that data constant and
/// reuse it over multiple frames. The other alternative is StreamBuffer, that is used to 
/// send data to the GPU that is only going to be used for drawing once. Both have broadly
/// equivalent functionality, and it is more an optimization issue of which one you use over
/// the other
class RBuffer : IIndexed {
   // Properties ---------------------------------------------------------------
   /// <summary>The list of all RBuffer</summary>
   public static IdxHeap<RBuffer> All = new ();

   /// <summary>IIndexed implementation of Idx</summary>
   public ushort Idx { get; set; }

   /// <summary>The reference count for this RBuffer (how many RBatch objects are pointing to it)</summary>
   public int References {
      get => mReferences;
      set {
         mReferences = value;
         if (value < 0) throw new InvalidOperationException ("Negative reference count for RBuffer");
         if (value == 0) Release ();
      }
   }
   int mReferences;

   /// <summary>GL handle to the VAO (allocated by PushToGPU)</summary>
   public HVertexArray VAO => mHVAO;
   HVertexArray mHVAO;

   /// <summary>The vertex specification for this RBuffer (layout of each vertex in it)</summary>
   public EVertexSpec VSpec { 
      get => mSpec;
      set => mcbVertex = Attrib.GetSize (mSpec = value); 
   }
   int mcbVertex;
   EVertexSpec mSpec;

   // Methods ------------------------------------------------------------------
   /// <summary>Add raw data into a RBuffer</summary>
   /// <param name="pSrc">Pointer to the data to add</param>
   /// <param name="cb">Count, in bytes, of the data</param>
   /// <returns>The index at which the first byte of data was added</returns>
   public unsafe int AddData (void* pSrc, int cb) {
      int n = mUsed;
      if (mUsed + cb > mData.Length)
         Array.Resize (ref mData, Math.Max (mUsed + cb, mData.Length * 2));
      fixed (void* pDst = &mData[mUsed])
         Buffer.MemoryCopy (pSrc, pDst, mData.Length, cb);
      mUsed += cb;
      return n;
   }

   /// <summary>Adds element indices into the Buffer, if we are using indexed-mode drawing</summary>
   public unsafe int AddIndices (ReadOnlySpan<int> seq) {
      int n = mIndexUsed, c = seq.Length;
      if (mIndexUsed + c > mIndex.Length)
         Array.Resize (ref mData, Math.Max (mIndexUsed + c, mData.Length * 2));
      seq.CopyTo (mIndex.AsSpan (mIndexUsed));
      mIndexUsed += c;
      return n;
   }

   /// <summary>Draws data from the VAO, after setting up vertex attributes</summary>
   /// This is a place-holder function which will get superseded by a better one later
   public void Draw (EMode mode, int offset, int count) {
      PushToGPU ();
      GL.DrawArrays (mode, offset / mcbVertex, count);
   }

   /// <summary>Gets a currently open RBuffer corresponding to a given vertex-spec</summary>
   /// An open retain-buffer is one that has not yet been pushed to the GPU, and is open
   /// for adding additional vertices into
   public static RBuffer Get (EVertexSpec spec) {
      RBuffer? rb = mBySpec[(int)spec];
      if (rb == null) (rb = mBySpec[(int)spec] = All.Alloc ()).VSpec = spec;
      return rb;
   }
   static RBuffer?[] mBySpec = new RBuffer?[(int)EVertexSpec._Last];

   // Implementation -----------------------------------------------------------
   // Release the VAO after use.
   // A VAO is released after all the RBatch objects pointing into it are
   // released (when this.References goes down to zero)
   public void Release () {
      if (GLState.VAO == mHVAO) GLState.VAO = 0;
      GL.DeleteBuffer (mHVertex); GL.DeleteBuffer (mHIndex); GL.DeleteVertexArray (mHVAO);
      mHVertex = mHIndex = HBuffer.Zero; mHVAO = HVertexArray.Zero;
      All.Release (Idx);
   }

   // Called to transmit the data to the GPU.
   // The first time this is called, it allocates a VAO (vertex-array-object), copies
   // the data into that and transmits it to the GPU. Subsequent calls simply bind the
   // VAO object as the current one to use 
   unsafe void PushToGPU () {
      if (mHVAO != 0) { GL.BindVertexArray (mHVAO); return; }
      GL.BindVertexArray (mHVAO = GL.GenVertexArray ());
      GL.BindBuffer (EBufferTarget.Array, mHVertex = GL.GenBuffer ());
      fixed (void* p = &mData[0])
         GL.BufferData (EBufferTarget.Array, mUsed, (Ptr)p, EBufferUsage.StaticDraw);

      GL.BindBuffer (EBufferTarget.ElementArray, mHIndex = GL.GenBuffer ());
      fixed (void* p = &mIndex[0])
         GL.BufferData (EBufferTarget.ElementArray, mIndexUsed * 4, (Ptr)p, EBufferUsage.StaticDraw);
      mData = null!; mIndex = null!; mBySpec[(int)VSpec] = null;
      mUsed = mIndexUsed = 0;

      int index = 0, offset = 0;
      var attribs = Attrib.GetFor (VSpec);
      foreach (var a in attribs) {
         if (a.Integral) GL.VertexAttribIPointer (index, a.Dims, a.Type, mcbVertex, offset);
         else GL.VertexAttribPointer (index, a.Dims, a.Type, false, mcbVertex, offset);
         GL.EnableVertexAttribArray (index);
         index++; offset += a.Size;
      }
   }

   // Private data -------------------------------------------------------------
   byte[] mData = new byte[1024];   // Raw data storage
   int mUsed;                       // How many bytes of that have we used
   int[] mIndex = new int[128];     // Indices storage
   int mIndexUsed;                  // How many elements of the Indices array are used

   HBuffer mHVertex;                // GL handle to the vertex data storage buffer
   HBuffer mHIndex;                 // GL handle to the index buffer (used only if indexed drawing)
}
#endregion

#region struct RBatch ------------------------------------------------------------------------------
/// <summary>Represents a batch of vertices being drawn</summary>
/// Ultimately, all drawing happens using RBatch objects. Each RBatch includes the following
/// information
/// - Which RBuffer contains the vertices to be drawn (NBuffer)
/// - Which shader program is used to draw (NShader)
/// - Which set of uniforms (within that shader) is being used (NUniform)
///   (this is the index within the mUniforms[] array maintained by that shader,
///   populated by that Shader's SnapUniforms method)
/// - Start and Count indicating the start offset and number of vertices in that
///   buffer, both in terms of 'vertex units', not in terms of bytes
struct RBatch : IIndexed {
   // Properties ---------------------------------------------------------------
   /// <summary>Implement IIndexed</summary>
   public ushort Idx { get; set; }

   /// <summary>The shader this RBatch uses</summary>
   public ushort NShader;
   /// <summary>Index of the RBuffer this points to</summary>
   /// Initially, the data pointed to by this RBatch is stored in the private
   /// mData area of the Shader, and at that time, the NBuffer is set to 0.
   /// Later, during the RBatch.Sort phase, the vertex data is moved 
   /// from the local storage in the Shader into RBuffer that we allocate at that
   /// point, and this then is the index of that RBuffer 
   public ushort NBuffer;
   /// <summary>Index of the uniform block (within that Shader)</summary>
   public ushort NUniform;

   /// <summary>Start position, in bytes, of this RBatch within the RBuffer</summary>
   /// In the initial phase (when we have not yet shifted the data into a RBuffer),
   /// this offset points into the mData maintained by that shader. After the data has
   /// moved into a RBuffer, the Offset points into that RBuffer
   public int Offset;
   /// <summary>Count of _vertices_ used by this batch</summary>
   /// Total number of bytes used for this batch is Count * Shader.CBVertex
   public int Count;

   /// <summary>As batches are being drawn, batches are accumulated here</summary>
   public static List<int> Staging = [];

   // Methods ------------------------------------------------------------------
   /// <summary>This allocates a new RBatch object (from the IdxHeap of RBatch that we manage)</summary>
   public static ref RBatch Alloc () => ref All.Alloc ();

   /// <summary>Returns a reference to the most recently allocated RBatch</summary>
   public static ref RBatch Recent () => ref All.Recent;

   /// <summary>Called when this RBatch is no longer in use, reduces our ref-count on the RBuffer</summary>
   /// Each RBuffer has a number of RBatch objects referring to it. As these RBatch
   /// objects go out of use, they decrement the reference count on the RBuffer, leading
   /// it to 
   public readonly void Release () {
      if (NBuffer != 0)
         RBuffer.All[NBuffer].References--;
      All.Release (Idx);
   }

   /// <summary>Extend this batch by a given number of extra vertices</summary>
   public void Extend (int delta) => Count = (ushort)(Count + delta);

   public readonly bool CanMerge (ref RBatch rb, int count) {
      // TODO: Check both belong to the same VModel?
      // TODO: Check ZLevel and ClipRect
      if (NShader != rb.NShader || NBuffer != rb.NBuffer) return false;
      var shader = Shader.Get (NShader);
      int n = shader.OrderUniforms (NUniform, rb.NUniform); if (n != 0) return false;
      return Offset + count * shader.CBVertex == rb.Offset;
   }

   /// <summary>This is called when we are starting a new frame</summary>
   /// It clears the staging area (that holds the RBatches we accumulate during
   /// this frame) as well as resetting soms stat couters (like the number of
   /// draw calls, number of vertices drawn etc)
   public static void StartFrame () {
      Staging.Clear ();
      mDrawCalls = mVertsDrawn = 0;
   }

   /// <summary>This is called to 'issue' this batch (the actual DrawArrays or DrawElements)</summary>
   public readonly void Issue (int count) {
      var shader = Shader.Get (NShader);
      // Select this program for use (if the program is already selected,
      // this is a no-op)
      GLState.Program = shader.Pgm;
      // Set the shader 'constants' - this is stuff like VPScale that does
      // not change during the frame rendering, and this actually does some
      // setting only once per frame, per shader
      shader.SetConstants (); 
      // Ask the shader to apply the uniforms for this batch
      shader.ApplyUniforms (NUniform);
      // Select the VAO this batch uses as the current VAO. If this VAO
      // is already selected, this is a no-op
      var buffer = RBuffer.All[NBuffer];
      GLState.VAO = buffer.VAO;
      // Finally draw 'count' vertices starting at this batch's vertex
      // offset (byte offset within that buffer). Note that we are not using
      // this RBatch's count, but the count is passed in from outside. This
      // is because IssueAll() sees if this batch and the subsequent one(s)
      // all use the same shader, VAO and uniforms and thus can be merged into
      // a larger single draw. 
      buffer.Draw (shader.Pgm.Mode, Offset, count);
      // Update stats
      mDrawCalls++; mVertsDrawn += count;
   }
   static internal int mDrawCalls, mVertsDrawn;

   /// <summary>This is called at the end of the frame to sort and issue all batches</summary>
   /// The Sort arranges the batches to minimize state changes. Then, we do
   /// an additional optimization by checking if there are successive batches
   /// here that can be combined into a single call ('larger virtual batch'),
   /// and that's why we pass in the count from here, rather than using each
   /// RBatch's count in Issue()
   public static void IssueAll () {
      Sort ();
      for (int n = Staging.Count, i = 0; i < n; i++) {
         ref RBatch ra = ref All[Staging[i]];
         int count = ra.Count;
         for (int j = i + 1; j < n; j++) {
            ref RBatch rb = ref All[Staging[j]];
            if (ra.CanMerge (ref rb, count)) { i = j; count += rb.Count; }
            else break;
         }
         ra.Issue (count);
      }
   }

   /// <summary>Release all the RBatch that are currently in the Staging queue</summary>
   /// This is a temporary function that will go away later, when each VNode
   /// maintains its own list of RBatch. Then, the lifetime of those RBatches
   /// is directly connected to the lifetime of the VNode
   public static void ReleaseAll () {
      foreach (var n in Staging) {
         ref RBatch rb = ref All[n];
         rb.Release ();
      }
   }

   // Implementation -----------------------------------------------------------
   // This is called to sort the RBatches before we draw them.
   // This sorts the batches with these keys (in descending order of importance):
   // - The shader program used
   // - The RBuffer from which the vertices are fetched
   // - The Uniforms used by that shader (so all Yellow quads sort together, for example)
   // - The Offset with the that RBuffer
   // This ordering represents the cost of changing state - it is much more expensive
   // to change shader programs, than to change the uniforms within a shader program,
   // for example.
   static void Sort () {
      Staging.Sort (Order);
      foreach (var n in Staging) {
         ref RBatch rb = ref All[n];
         if (rb.NBuffer == 0) {
            // If the data of this RBatch has still not been uploaded to the GPU,
            // allocate a RB,,uffer and copy the data there
            var shader = Shader.Get (rb.NShader);
            var buf = RBuffer.Get (EVertexSpec.Vec2F); // TODO: Use the correct vertex-spec
            rb.NBuffer = buf.Idx;
            rb.Offset = shader.CopyVertices (buf, rb.Offset, rb.Count);
            buf.References++;
         }
      }

      // Helper .................................
      // Compares two RBatch, given their index
      static int Order (int a, int b) {
         ref RBatch ra = ref All[a], rb = ref All[b];
         int n = ra.NShader - rb.NShader; if (n != 0) return n;
         n = ra.NBuffer - rb.NBuffer; if (n != 0) return n;
         var shader = Shader.Get (ra.NShader);
         n = shader.OrderUniforms (ra.NUniform, rb.NUniform);
         if (n != 0) return n;
         return ra.Offset - rb.Offset;
      }
   }

   public override readonly string ToString () {
      if (Count == 0) return "Empty";
      else {
         var shader = Shader.Get (NShader);
         string uniform = shader.DescribeUniforms (NUniform);
         return $"{shader.Pgm.Name}[{uniform}]  {Count} @ {Offset / shader.CBVertex}";
      }
   }

   // The indexed heap of all the RBatch
   static IdxHeap<RBatch> All = new ();
}
#endregion
