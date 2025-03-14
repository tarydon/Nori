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
         Array.Resize (ref mIndex, Math.Max (mIndexUsed + c, mIndex.Length * 2));
      seq.CopyTo (mIndex.AsSpan (mIndexUsed));
      mIndexUsed += c;
      return n;
   }

   /// <summary>Draws data from the VAO using a simple DrawArrays call</summary>
   public void Draw (EMode mode, int offset, int count) {
      PushToGPU ();
      GL.DrawArrays (mode, offset / mcbVertex, count);
   }

   /// <summary>Draws data from the VAO using a more complex DrawElements call (indexed drawing)</summary>
   public void Draw (EMode mode, int offset, int ioffset, int icount) {
      PushToGPU ();
      GL.DrawElementsBaseVertex (mode, icount, EIndexType.UInt, ioffset * 4, offset / mcbVertex);
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
