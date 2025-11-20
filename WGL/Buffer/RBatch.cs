// ────── ╔╗
// ╔═╦╦═╦╦╬╣ RBatch.cs
// ║║║║╬║╔╣║ Implements RBatch, representing a set of vertices in a RBuffer
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

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
   public int Idx1 { get; set; }

   /// <summary>The VNode to which this batch belongs</summary>
   /// We need this to avoid merging batches belonging to different VNodes
   /// together (since each needs to be stored in the respective VNode's batch list)
   public ushort IDVNode;

   /// <summary>The shader this RBatch uses</summary>
   public ushort NShader;
   /// <summary>Index of the RBuffer this points to</summary>
   /// Initially, the data pointed to by this RBatch is stored in the private
   /// mData area of the Shader, and at that time, the NBuffer is set to 0.
   /// Later, during the RBatch.Sort phase, the vertex data is moved
   /// from the local storage in the Shader into RBuffer that we allocate at that
   /// point, and this then is the index of that RBuffer
   public int NBuffer;
   /// <summary>Index of the uniform block (within that Shader)</summary>
   /// This is transient storage, used only until this batch is stored in the Batches
   /// list of a VNode. That's because the same RBatch might be used multiple times
   /// in a SceneGraph (instancing of the same VNode multiple times in the graph),
   /// each time with typically different transforms (and thus a different set of
   /// uniforms).
   public ushort NUniform;

   /// <summary>'ZLevel' is like a sort order for RBatch (they are drawn from min..max order)</summary>
   public short ZLevel;

   /// <summary>Start position, in bytes, of this RBatch within the RBuffer</summary>
   /// In the initial phase (when we have not yet shifted the data into a RBuffer),
   /// this offset points into the mData maintained by that shader, and at that point,
   /// it is an INDEX. After the data has moved into a RBuffer, the Offset points
   /// into that RBuffer, and at that point it is a BYTE-OFFSET
   public int Offset;
   /// <summary>Count of _vertices_ used by this batch</summary>
   /// Total number of bytes used for this batch is Count * Shader.CBVertex
   public int Count;

   /// <summary>Start offset of this batch within the index buffer (if we're using indices)</summary>
   public int IOffset;
   /// <summary>Count of indices used for this batch (0 if we are not using indexed drawing)</summary>
   public int ICount;

   /// <summary>Staging area to collect all the batches from all the VNodes (during each frame)</summary>
   /// See VNode.Render() for a full explanation. In short, during EACH FRAME, we flush this
   /// list, and ask all visible vnodes to add their draw batches into this. Then this list
   /// is sorted (to minimize state changes) and drawn (see RBatch.IssueAll for that).
   /// NOTE: This list contains a list of batches, each associated with a set of attributes
   /// (as stored in the Uniform value). This uniform is the index of a Uniforms struct peculiar
   /// to that shader, and stored in that Shader's Uniforms[] list.
   public static List<(int Batch, ushort Uniform)> Staging = [];

   // Methods ------------------------------------------------------------------
   /// <summary>This allocates a new RBatch object (from the IdxHeap of RBatch that we manage)</summary>
   public static ref RBatch Alloc () => ref mAll.Alloc ();

   /// <summary>This is called at the end of the frame to sort and issue all batches</summary>
   /// The Sort arranges the batches to minimize state changes. Then, we do
   /// an additional optimization by checking if there are successive batches
   /// here that can be combined into a single call ('larger virtual batch'),
   /// and that's why we pass in the count from here, rather than using each
   /// RBatch's count in Issue()
   public static void IssueAll () {
      Sort ();
      for (int n = Staging.Count, i = 0; i < n; i++) {
         var (b0, u0) = Staging[i];
         ref RBatch rb0 = ref mAll[b0];
         int count = rb0.Count;
         for (int j = i + 1; j < n; j++) {
            var (b1, u1) = Staging[j];
            ref RBatch rb1 = ref mAll[b1];
            if (rb0.CanMerge (ref rb1, count, u0, u1)) { i = j; count += rb1.Count; } else break;
         }
         rb0.Issue (u0, count);
      }
   }

   /// <summary>Extend this batch by a given number of extra vertices</summary>
   public void Extend (int delta) => Count = (ushort)(Count + delta);

   /// <summary>Returns a reference to the nth RBatch (from the indexed list mAll of all batches)</summary>
   public static ref RBatch Get (int n) => ref mAll[n];
   static IdxHeap<RBatch> mAll = new ();

   /// <summary>Returns a reference to the most recently allocated RBatch</summary>
   public static ref RBatch Recent () => ref mAll.Recent;

   /// <summary>Called when this RBatch is no longer in use, reduces our ref-count on the RBuffer</summary>
   /// Each RBuffer has a number of RBatch objects referring to it. As these RBatch objects go out
   /// of use, they decrement the reference count on the RBuffer, and it eventually gets deleted
   public readonly void Release () {
      if (NBuffer != 0)
         RBuffer.All[NBuffer].References--;
      mAll.Release (Idx1);
   }

   /// <summary>This is called when we are starting a new frame</summary>
   /// It clears the staging area (that holds the RBatches we accumulate during
   /// this frame) as well as resetting soms stat couters (like the number of
   /// draw calls, number of vertices drawn etc)
   public static void StartFrame () {
      Staging.Clear ();
      mDrawCalls = mVertsDrawn = 0;
   }

   // Implementation -----------------------------------------------------------
   // This checks if this batch can be merged with another RBatch rb1.
   // We are going to draw this batch with Uniforms[uni0], and the batch rb1
   // with Uniforms[un1]. Note that since this is done during the IssueAll phase, and
   // not when gathering the batches for storage within the VNode, we don't care if these
   // two RBatch belong to different VNodes.
   readonly bool CanMerge (ref RBatch rb1, int count, ushort uni0, ushort uni1) {
      if (NShader != rb1.NShader || NBuffer != rb1.NBuffer || ZLevel != rb1.ZLevel) return false;
      // In Pick mode, don't merge two batches that belong to different VNodes (we are going
      // to draw this batch with a false-color that effectively encodes the VNode ID so we don't
      // want them getting mixed up
      if (Lux.IsPicking && IDVNode != rb1.IDVNode) return false;
      // Don't merge two RBatch that use indexed drawing
      if (ICount > 0 || rb1.ICount > 0) return false;
      // If both are not using the same uniforms, we can't merge. We can't
      // just compare the uniform indices here, we have to ask the shader to compare
      // if these two sets of uniforms are actually identical
      var shader = Shader.Get (NShader);
      int n = shader.OrderUniforms (uni0, uni1); if (n != 0) return false;
      // Finally, the two batches storage should be back to back so that
      // rb1's storage followed immediately after this one
      if (NBuffer == 0) return Offset + count == rb1.Offset;
      else return Offset + count * shader.CBVertex == rb1.Offset;
   }

   // <summary>This is called to 'issue' this batch (the actual DrawArrays or DrawElements)</summary>
   readonly void Issue (ushort nUniform, int count) {
      var shader = Shader.Get (NShader);
      if (!Lux.IsPicking) {
         // Select the program used by this batch. If this same program has already
         // been selected, this is a no-op. Because we have already sorted the batches by
         // program, these expensive changes to the current pipeline are quite rare
         GLState.Program = shader.Pgm;
         // Set the shader 'constants' - this is stuff like VPScale that does
         // not change during the frame rendering, and this actually does some
         // setting only once per frame, per shader
         shader.SetConstants ();
         // Ask the shader to apply the uniforms for this batch
         shader.ApplyUniforms (nUniform);
      } else {
         // In pick mode, everything except meshes drawn by the facet shaders can be
         // ignored (we may relax this later)
         if (shader is not FacetShader fsh) return;
         // In pick mode, we switch to using the PickShader (which draws the triangles
         // using a 'false-color' which is basically just the VNode Id
         var (picker, uniforms) = (PickShader.It, fsh.GetUniforms (nUniform));
         Color4 color = Color4.White;
         if (VNode.SafeGet (IDVNode) is { } vnode) { 
            // Compute the false color based on the VNode Id. Note that we are not using
            // the lowest two bits of R, G, B in this (to work correctly even with display
            // modes that use restricted colors with just 6 bits per color component). 
            int r = (vnode.Id & 63) << 2, g = (vnode.Id >> 4) & 252, b = (vnode.Id >> 10) & 252;
            color = new (r, g, b);
         }
         GLState.Program = picker.Pgm;
         PickShader.It.ApplyUniforms (uniforms.IDXfm, color);
      }
      // Select the VAO this batch uses as the current VAO. If this VAO
      // is already selected, this is a no-op
      var buffer = RBuffer.All[NBuffer];
      GLState.VAO = buffer.VAO;

      if (ICount > 0) {
         // If we are using indexed drawing mode, we ignore the count that is passed
         // in, and use this.ICount as the number of elements to draw
         buffer.Draw (shader.Pgm.Mode, Offset, IOffset, ICount);
      } else {
         // If ICount = 0: we are using simple DrawArrays.
         // We have to draw 'count' vertices starting at this batch's vertex
         // offset (byte offset within that buffer). Note that we are not using
         // this RBatch's count, but the count is passed in from outside. This
         // is because IssueAll() sees if this batch and the subsequent one(s)
         // all use the same shader, VAO and uniforms and thus can be merged into
         // a larger single draw.
         buffer.Draw (shader.Pgm.Mode, Offset, count);
         mVertsDrawn += count;
      }
      // Update stats
      mDrawCalls++;
   }
   internal static int mDrawCalls, mVertsDrawn;

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
      // Sort the Staging array - since the sorting is just an optimization to minimize
      // state changes, we don't want to spend too much time sorting. If the number of
      // batches in the Staging array is very large (tens of thousands), then the Sort
      // itself will take too much time (since it is O(n log n)). To avoid this, we don't
      // sort the entire array, but sort only 'batch-sized' sections of it.
      // Some experimentation shows that a batch size of between 200 to 600 seems to work
      // the best. For now, this is 256. As the batch size increases, sort time will increase.
      // As the batch size decreases, OpenGL state-transition time will increase (we'll spend
      // more time swapping between shaders, uniforms etc).
      int batch = 256;
      int n = (Staging.Count + batch - 1) / batch;
      for (int i = 0; i < n; i++) {
         int a = i * batch, b = Math.Min (batch, Staging.Count - a);
         Staging.Sort (a, b, RBatchCompare.It);
      }

      // If any of these batches point to data that has not yet been loaded into an
      // RBuffer, we will do that here.
      foreach (var (b, _) in Staging) {
         ref RBatch rb = ref mAll[b];
         if (rb.NBuffer == 0) {
            // If the data of this RBatch has still not been uploaded to the GPU,
            // allocate a RB,,uffer and copy the data there
            var shader = Shader.Get (rb.NShader);
            var buf = RBuffer.Get (shader.Pgm.VSpec);
            rb.NBuffer = buf.Idx1;
            if (rb.ICount > 0)
               (rb.Offset, rb.IOffset) = shader.CopyVertices (buf, rb.Offset, rb.Count, rb.IOffset, rb.ICount);
            else
               rb.Offset = shader.CopyVertices (buf, rb.Offset, rb.Count);
            buf.References++;
         }
      }
   }

   public readonly override string ToString () {
      if (Count == 0) return "Empty";
      else {
         var shader = Shader.Get (NShader);
         string uniform = shader.DescribeUniforms (NUniform);
         return $"{shader.Pgm.Name}[{uniform}]  {Count} @ {Offset / shader.CBVertex}";
      }
   }

   // Nested types -------------------------------------------------------------
   // IComparer implementation used to sort the Staging area
   class RBatchCompare : IComparer<(int B, ushort U)> {
      public int Compare ((int B, ushort U) ub0, (int B, ushort U) ub1) {
         ref RBatch ra = ref mAll[ub0.B], rb = ref mAll[ub1.B];
         int n = ra.ZLevel - rb.ZLevel; if (n != 0) return n;
         if (ra.NShader != rb.NShader) {
            Shader s1 = Shader.Get (ra.NShader), s2 = Shader.Get (rb.NShader);
            return s1.SortCode - s2.SortCode;
         }
         if (n != 0) return n;
         n = ra.NBuffer - rb.NBuffer; if (n != 0) return n;
         var shader = Shader.Get (ra.NShader);
         n = shader.OrderUniforms (ub0.U, ub1.U);
         if (n != 0) return n;
         return ra.Offset - rb.Offset;
      }
      internal static readonly RBatchCompare It = new ();
   }
}
#endregion
