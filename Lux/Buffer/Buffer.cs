// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Buffer.cs
// ║║║║╬║╔╣║ Implements RetainBuffer (VAO wrapper), StreamBuffer (streaming GL buffer)
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;
using Ptr = nint;

#region class RetainBuffer -------------------------------------------------------------------------
/// <summary>A wrapper around a VertexArrayObject (VAO), used for 'retained mode' drawing</summary>
/// We can store vertex data in a RetainBuffer, if we intend to keep that data constant and
/// reuse it over multiple frames. The other alternative is StreamBuffer, that is used to
/// send data to the GPU that is only going to be used for drawing once. Both have broadly
/// equivalent functionality, and it is more an optimization issue of which one you use over
/// the other
class RetainBuffer : IIndexed {
   // Properties ---------------------------------------------------------------
   /// <summary>The list of all RetainBuffers</summary>
   public static IdxHeap<RetainBuffer> All = new ();

   /// <summary>IIndexed implementation of Idx</summary>
   public int Idx { get; set; }

   /// <summary>The reference count for this buffer (how many RBatch objects are pointing to it)</summary>
   public int References {
      get => mReferences;
      set {
         mReferences = value;
         if (value < 0) throw new InvalidOperationException ("Negative reference count for RetainBuffer");
         if (value == 0) Release ();
      }
   }
   int mReferences;

   /// <summary>GL handle to the VAO (allocated by PushToGPU)</summary>
   public HVertexArray VAO => mHVAO;
   HVertexArray mHVAO;

   /// <summary>The vertex specification for this buffer (layout of each vertex in it)</summary>
   public EVertexSpec VSpec {
      get => mSpec;
      set => mcbVertex = Attrib.GetSize (mSpec = value);
   }
   int mcbVertex;
   EVertexSpec mSpec;

   // Methods ------------------------------------------------------------------
   /// <summary>Add raw data into a RetainBuffer</summary>
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
   public int AddIndices (ReadOnlySpan<int> seq) {
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

   /// <summary>Gets a currently open RetainBuffer corresponding to a given vertex-spec</summary>
   /// An open retain-buffer is one that has not yet been pushed to the GPU, and is open
   /// for adding additional vertices into
   public static RetainBuffer Get (EVertexSpec spec) {
      RetainBuffer? rb = mBySpec[(int)spec];
      if (rb == null) (rb = mBySpec[(int)spec] = All.Alloc ()).VSpec = spec;
      return rb;
   }
   static readonly RetainBuffer?[] mBySpec = new RetainBuffer?[(int)EVertexSpec._Last];

   // Implementation -----------------------------------------------------------
   // Release the VAO after use.
   // A VAO is released after all the RBatch objects pointing into it are
   // released (when this.References goes down to zero)
   public void Release () {
      if (GLState.VAO == mHVAO) GLState.VAO = 0;
      GL.DeleteBuffer (mHVertex); GL.DeleteBuffer (mHIndex); GL.DeleteVertexArray (mHVAO);
      mHVertex = mHIndex = HBuffer.Zero; mHVAO = HVertexArray.Zero;
      if (Idx == 2) Debug.WriteLine ($"Release {Idx}"); // REMOVETHIS
      All.Release (Idx);
   }

   // Called to transmit the data to the GPU.
   // The first time this is called, it allocates a VAO (vertex-array-object), copies
   // the data into that and transmits it to the GPU. Subsequent calls simply bind the
   // VAO object as the current one to use
   unsafe void PushToGPU () {
      if (mHVAO != 0) { GLState.VAO = mHVAO; return; }
      GLState.VAO = mHVAO = GL.GenVertexArray ();
      GL.BindBuffer (EBufferTarget.Array, mHVertex = GL.GenBuffer ());
      fixed (void* p = &mData[0])
         GL.BufferData (EBufferTarget.Array, mUsed, (Ptr)p, EBufferUsage.StaticDraw);
      PushedVerts = mUsed;

      GL.BindBuffer (EBufferTarget.ElementArray, mHIndex = GL.GenBuffer ());
      fixed (void* p = &mIndex[0])
         GL.BufferData (EBufferTarget.ElementArray, mIndexUsed * 4, (Ptr)p, EBufferUsage.StaticDraw);
      mData = null!; mIndex = null!; mBySpec[(int)VSpec] = null;
      mUsed = mIndexUsed = 0;

      // Note that doing all this attribute-setting here works because all this is part of
      // the state of the currently selected VAO. That is, the VAO not only tags a particular
      // 'vertex buffer' and a particular 'index buffer' as being the current ones, but also
      // set up the set of vertex attributes (the type of each component of the 'vertex' 
      // structure) and enables as many vertex attributes as we are using. 
      int index = 0, offset = 0;
      var attribs = Attrib.GetFor (VSpec);
      foreach (var a in attribs) {
         if (a.Integral) GL.VertexAttribIPointer (index, a.Dims, a.Type, mcbVertex, offset);
         else GL.VertexAttribPointer (index, a.Dims, a.Type, false, mcbVertex, offset);
         GL.EnableVertexAttribArray (index);
         index++; offset += a.Size;
      }
      PushID = ++mNextPushID;
   }
   public int PushID, PushedVerts;
   static int mNextPushID;

   public override string ToString () 
      => $"RBuffer Idx:{Idx}, Spec:{VSpec}, Push:{PushedVerts} bytes @ {PushID}";

   // Private data -------------------------------------------------------------
   byte[] mData = new byte[1024];   // Raw data storage
   int mUsed;                       // How many bytes of that have we used
   int[] mIndex = new int[128];     // Indices storage
   int mIndexUsed;                  // How many elements of the Indices array are used

   HBuffer mHVertex;                // GL handle to the vertex data storage buffer
   HBuffer mHIndex;                 // GL handle to the index buffer (used only if indexed drawing)
}
#endregion

#region class StreamBuffer -------------------------------------------------------------------------
/// <summary>StreamBuffer implements lock-free streaming to OpenGL</summary>
/// For full details on this, see the "Buffer Object Streaming" page on the Khronos OpenGL
/// Wiki (https://www.khronos.org/opengl/wiki/Buffer_Object_Streaming). In particular, note
/// the section on "Buffer Update". 
/// In short, this is what we do:
/// - We create a buffer of a fixed size (8MB, for example), with the StreamDraw usage
/// - Each time we want to make a DrawArrays call, we call glMapBufferRange with the 
///   GL_MAP_UNSYNCHRONIZED_BIT. This tells OpenGL not to do any synchronization _at all_.
///   We're telling OpenGL: "Please give me write access to a region of this buffer immediately.
///   I promise not to overwrite any of the areas you might still be reading / executing."
/// - We maintain a 'cursor' into this buffer, initially at 0. Each time we write, we advance
///   the cursor by the size of data written (rounding up to 64, which is the minimum size GL
///   needs to maintain UNSYNCHRONIZED access). 
/// - If the fresh data we need to write is too big to fit into the remaining space (8MB - cursor),
///   we simply 'orphan' this buffer (by calling BufferData with the same size, and passing NULL).
///   This tells OpenGL: allocate a fresh 8MB for me to write in, while you can continue reading from
///   the previous data allocated for this buffer. When we Orphan, we set cursor back to 0 and start
///   writing from the start of this fresh new buffer we got. 
/// - Eventually, there will be a few 8MB buffers 'in flight' with data we've written but which
///   OpenGL is still rendering. Since all the buffers are exactly the same size, it makes it very
///   easy for the driver to optimize it's heap management and we are usually going to get to a 
///   steady state soon where there are N 8MB buffers continuously being rotated between the CPU
///   and the GPU. 
/// In practice, this is really efficient and we are getting frame rates (even with a very large
/// number of small draw primitives) that are well beyond what even the Flux rendering engine can
/// manage. 
/// Note: The ShaderWrap classes add another level of optimization on top of this StreamBuffer. 
class StreamBuffer {
   // Constructors -------------------------------------------------------------
   /// <summary>Construct a StreamBuffer (this generates the buffer and assigns 8MB of storage for it)</summary>
   public StreamBuffer () {
      mId = GL.GenBuffer ();
      GL.BindBuffer (EBufferTarget.Array, mId);
      GL.BufferData (EBufferTarget.Array, mSize = 8192 * 1024, 0, EBufferUsage.StreamDraw);
   }

   public static StreamBuffer It => mIt ??= new ();
   static StreamBuffer? mIt;

   // Methods ------------------------------------------------------------------
   /// <summary>Copy data into the buffer from the given pointer, and issue a DrawArrays call</summary>
   /// <param name="shader">The shader we're currently using (we use this to get the mode)</param>
   /// <param name="pSrc">The source buffer from where the 'vertex definitions' are picked</param>
   /// <param name="nVerts">The number of 'vertices'</param>
   /// <param name="attribs">The set of Attrib values (like Vec4f, int, Vec2s etc)</param>
   internal unsafe void Draw (ShaderImp shader, void* pSrc, int nVerts, Attrib[] attribs) {
      GLState.VAO = HVertexArray.Zero;
      GL.BindBuffer (EBufferTarget.Array, mId);
      int cbVertex = attribs.Sum (a => a.Size);
      int cbData = cbVertex * nVerts, cbReserve = cbData.RoundUp (64);
      if (cbReserve > mSize) throw new Exception ($"StreamBuffer size of {mSize} bytes inadequate.");
      if (mCursor + cbReserve > mSize) Orphan ();
      Ptr pDst = GL.MapBufferRange (EBufferTarget.Array, mCursor, cbReserve, EMapAccess.Unsynchronized | EMapAccess.Write);
      Buffer.MemoryCopy (pSrc, pDst.ToPointer (), cbData, cbData);
      GL.UnmapBuffer (EBufferTarget.Array);

      // If the set of attributes has changed, then we set up the attribute array again
      int index = 0, basis = mCursor;
      foreach (var a in attribs) {
         if (a.Integral) GL.VertexAttribIPointer (index, a.Dims, a.Type, cbVertex, basis);
         else GL.VertexAttribPointer (index, a.Dims, a.Type, false, cbVertex, basis);
         GL.EnableVertexAttribArray (index);
         index++; basis += a.Size;
      }

      mCursor += cbReserve;
      GL.DrawArrays (shader.Mode, 0, nVerts);
      for (int i = 0; i < index; i++) GL.DisableVertexAttribArray (index);
      GL.BindBuffer (EBufferTarget.Array, HBuffer.Zero);
   }

   // Implementation -----------------------------------------------------------
   // This is called when the buffer is full - Calling GL.BufferData with a null Ptr value
   // tells GL that we are done with this buffer and to submit it to rendering. This also
   // allocates a fresh 8MB buffer for us to start filling, and sets the cursor back to 0.
   void Orphan () {
      mCursor = 0;
      GL.BufferData (EBufferTarget.Array, mSize, 0, EBufferUsage.StreamDraw);
   }

   readonly HBuffer mId;      // The buffer we're using
   readonly int mSize;        // Size of that buffer
   int mCursor;               // Current write-cursor position in that buffer
}
#endregion
