namespace Nori;
using Ptr = nint;

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
      Debug.Print ($"Cursor = {mCursor}");
   }

   // Implementation -----------------------------------------------------------
   void Orphan () {
      mCursor = 0;
      GL.BufferData (EBufferTarget.Array, mSize, 0, EBufferUsage.StreamDraw);
   }

   readonly HBuffer mId;      // The buffer we're using
   readonly int mSize;        // Size of that buffer
   int mCursor;               // Current write-cursor position in that buffer
}
#endregion
