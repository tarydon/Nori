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
class RBuffer {
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

   /// <summary>This adds data into a RBuffer, and creates an RBatch wrapping this section of the buffer</summary>
   public unsafe static void AddData<T> (Shader shader, ReadOnlySpan<T> data) where T : unmanaged {
      It ??= new ();
      fixed (void* p = &data[0]) {
         int start = It.AddData (p, data.Length * Marshal.SizeOf<T> ());
         RBatch.All.Add (new RBatch (It, shader, start, data.Length));  // POI. start in bytes, count in vertices
      }
   }

   /// <summary>TEMPORARY - we are going to use only one RBuffer, keeping it alive only for one frame</summary>
   public static RBuffer? It;

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
   public void Draw (EMode mode, IReadOnlyList<ShaderImp.AttributeInfo> attribs, int cbVertex, int offset, int count) {
      PushToGPU ();
      int index = 0;
      foreach (var a in attribs) {
         if (a.Integral) GL.VertexAttribIPointer (index, a.Dimensions, a.ElemType, cbVertex, offset);
         else GL.VertexAttribPointer (index, a.Dimensions, a.ElemType, false, cbVertex, offset);
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
      if (GLState.VAO == mHVAO) GLState.VAO = 0; 
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

#region class RBatch -------------------------------------------------------------------------------
/// <summary>RBatch is like a 'slice' of a RBuffer representing one batch of draw commands</summary>
class RBatch {
   public RBatch (RBuffer buffer, Shader shader, int start, int count)
      => (Buffer, Shader, Start, Count, Uniforms) = (buffer, shader, start, count, shader.SnapUniforms ());

   public readonly RBuffer Buffer;
   public readonly Shader Shader;
   public readonly int Start;
   public readonly int Count;
   public readonly object Uniforms; // POI.

   public static List<RBatch> All = [];

   public void Issue () {
      Shader.ApplyUniforms (Uniforms);
      var pgm = Shader.Program;
      pgm.Use ();
      Buffer.Draw (pgm.Mode, pgm.Attributes, pgm.CBVertex, Start, Count);
   }
}
#endregion
