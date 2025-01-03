// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ RBuffer.cs
// ║║║║╬║╔╣║ Implements the 'retained buffer' flavor of VAO (Vertex Array Object)
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
class RetainBuffer {
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

   /// <summary>Another variant of AddData that adds the contents of a ReadOnlySpan of T</summary>
   /// You can add data from a List of T by using the .AsSpan() extension method to 
   /// get a ReadOnlySpan _view_ of the List (without making a copy)
   public unsafe int AddData2<T> (ReadOnlySpan<T> data) where T : unmanaged {
      fixed (void* p = &data[0])
         return AddData (p, data.Length * Marshal.SizeOf<T> ());
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
   public void Draw (EMode mode, Attrib[] attribs, int cbVertex, int offset, int count) {
      PushToGPU ();
      int index = 0;
      foreach (var a in attribs) {
         if (a.Integral) GL.VertexAttribIPointer (index, a.Dims, a.Type, cbVertex, offset);
         else GL.VertexAttribPointer (index, a.Dims, a.Type, false, cbVertex, offset);
         GL.EnableVertexAttribArray (index);
         index++; offset += a.Size;
      }
      GL.DrawArrays (mode, 0, count);
   }

   /// <summary>Called to transmit the data to the GPU</summary>
   /// The first time this is called, it allocates a VAO (vertex-array-object), copies
   /// the data into that and transmits it to the GPU. Subsequent calls simply bind the
   /// VAO object as the current one to use 
   public unsafe void PushToGPU () {
      if (mHVAO != 0) { GL.BindVertexArray (mHVAO); return; }
      GL.BindVertexArray (mHVAO = GL.GenVertexArray ());
      GL.BindBuffer (EBufferTarget.Array, mHVertex = GL.GenBuffer ());
      fixed (void* p = &mData[0])
         GL.BufferData (EBufferTarget.Array, mUsed, (Ptr)p, EBufferUsage.StaticDraw);

      GL.BindBuffer (EBufferTarget.ElementArray, mHIndex = GL.GenBuffer ());
      fixed (void* p = &mIndex[0])
         GL.BufferData (EBufferTarget.ElementArray, mIndexUsed * 4, (Ptr)p, EBufferUsage.StaticDraw);
      mData = null!; mIndex = null!; 
      mUsed = mIndexUsed = 0;
   }

   /// <summary>Release the VAO after use</summary>
   public void Release () {
      GL.BindVertexArray (HVertexArray.Zero);   // TODO: Only if this is the current VAO?
      GL.DeleteBuffer (mHVertex); GL.DeleteBuffer (mHIndex); GL.DeleteVertexArray (mHVAO);
      mHVertex = mHIndex = HBuffer.Zero; mHVAO = HVertexArray.Zero;
   }

   // Private data -------------------------------------------------------------
   byte[] mData = new byte[1024];   // Raw data storage
   int mUsed;                       // How many bytes of that have we used
   int[] mIndex = new int[128];     // Indices storage
   int mIndexUsed;                  // How many elements of the Indices array are used

   HVertexArray mHVAO;              // GL handle to the VAO (allocated by PushToGPU)
   HBuffer mHVertex;                // GL handle to the vertex data storage buffer
   HBuffer mHIndex;                 // GL handle to the index buffer (used only if indexed drawing)
}
#endregion
