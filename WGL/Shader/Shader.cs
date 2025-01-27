// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ Shader.cs
// ║║║║╬║╔╣║ Temporary code - preparing for Shader<T, U>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
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
      Idx = (ushort)mAll.Count; mAll.Add (this);
   }

   // Properties --------------------l-------------------------------------------
   /// <summary>Returns the size of each vertex (the sum of sizes of the Attrib array)</summary>
   public readonly int CBVertex;

   /// <summary>The index of this Shader (used for RBatch.NShader)</summary>
   public readonly ushort Idx;

   /// <summary>The underlying shader program this wraps around</summary>
   public readonly ShaderImp Pgm;

   // Methods ------------------------------------------------------------------
   /// <summary>Gets a shader, given its index</summary>
   public static Shader Get (int index) => mAll[index];

   /// <summary>This is called for every shader at the start of every frame</summary>
   public static void StartFrame () {
      mAll.ForEach (a => a?.Cleanup ());
      mApplyUniforms = 0;
   }
   protected internal static int mApplyUniforms;
   protected internal static int mSetConstants;

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
   /// When we create the draw calls (using Pix.Lines, Pix.Mesh etc), the data is first
   /// gathered in the respective mData buffers of each shader. Then later when the entire
   /// scene has been thus drawn, we move all this data into RBuffer objects using this method.
   public abstract int CopyVertices (RBuffer buffer, int start, int count);

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
   /// like Pix.DrawColor, Pix.LineWidth into the UBlock of that shader. Since each 
   /// shader uses a different set of uniforms, this has to be a virtual function. 
   /// This returns the index of the UBlock with that shader's Uniforms list. Later,
   /// that can be applied into a shader program by calling ApplyUniforms(int). 
   public abstract ushort SnapUniforms ();

   // Private data -------------------------------------------------------------
   // The list of all shaders - Shader.Idx indexes into this list
   protected static List<Shader> mAll = [null];
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
      ushort nUniform = SnapUniforms ();
      if (!ExtendBatch (data.Length)) {
         ref RBatch rb = ref RBatch.Alloc ();
         rb.NShader = Idx; rb.NUniform = nUniform; rb.NBuffer = 0;
         rb.Offset = mData.Count; rb.Count = data.Length;
         RBatch.Staging.Add (rb.Idx);
      }
      mData.AddRange (data);

      // Helper fuction to see if we can just extend the last batch we added to 
      // include these vertices as well. For this to work:
      // - The previous batch should use the same shader (this)
      // - The previous batch should be using the same set of uniforms
      bool ExtendBatch (int delta) {
         // TODO: Ensure the two RBatch belong to the same VNode
         ref RBatch rb = ref RBatch.Recent ();
         if (rb.NShader != Idx || rb.NUniform != nUniform || rb.NBuffer != 0) return false;
         rb.Extend (delta);
         return true; 
      }
   }

   // Overrides ----------------------------------------------------------------
   /// <summary>Copies vertices from our local mData storage to an RBuffer</summary>
   /// This copies 'count' vertices from our local mData storage into the given
   /// RBuffer. This means effectively 'count * CBVertex' bytes of data, This returns
   /// the byte offset within the RBuffer where the data has been copied.
   public unsafe override int CopyVertices (RBuffer buffer, int offset, int count) {
      var span = CollectionsMarshal.AsSpan (mData);
      fixed (void* p = &span[offset])
         return buffer.AddData (p, count * CBVertex);
   }

   /// <summary>Describe a particular uniform in human readable way (used for debugging)</summary>
   /// This description forms part of an RBatch.ToString() description
   public override string DescribeUniforms (int id)
      => mUniforms[id]?.ToString () ?? "";

   /// <summary>Override this to compare the 'uniform data' of two batches</summary>
   /// This must provide a definitive ordering, to ensure that all batches with similar
   /// uniforms get grouped together so that we reduce the number of issues
   /// TODO: Make this use ref UBlock?
   abstract protected int OrderUniformsImp (ref readonly TUniform a, ref readonly TUniform b);

   public override int OrderUniforms (int id1, int id2) {
      var span = mUniforms.AsSpan ();
      ref readonly TUniform ub1 = ref span[id1], ub2 = ref span[id2];
      return OrderUniformsImp (in ub1, in ub2);
   }

   /// <summary>Override this to set up the uniforms for this batch</summary>
   /// TODO: Make this use ref UBlock?
   abstract protected void ApplyUniformsImp (ref readonly TUniform settings);

   public override void ApplyUniforms (int nUniform) {
      var span = mUniforms.AsSpan ();
      ref readonly TUniform ub = ref span[nUniform];
      ApplyUniformsImp (in ub);
      mApplyUniforms++;
   }

   /// <summary>Helper used by SnapUniforms to do the actual encapsulation of uniforms into a UBlock object</summary>
   abstract protected TUniform SnapUniformsImp ();

   /// <summary>Helper used by SetConstants to do the actual setting of constants</summary>
   abstract protected void SetConstantsImp ();

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
            if (id == -1) throw new Exception ($"Uniform '{f.Name[2..]}' not found in shader '{Pgm.Name}'");
            f.SetValue (this, id);
         }
   }

   /// <summary>Called at the end of every frame</summary>
   public override void Cleanup () { mUniforms.Clear (); mData.Clear (); }

   /// <summary>Implementation of IComparer interface</summary>
   public int Compare (TUniform? a, TUniform? b) => OrderUniformsImp (in a!, in b!);

   /// <summary>Set the constants (like viewport size) that don't change during the entire frame</summary>
   public sealed override void SetConstants () {
      // If this shader has already been used in this frame (mRung2 == Pix.Rung),
      // then the constants have already been set, and we don't need ot set them again
      if (!Lib.Set (ref mRung2, Pix.Rung)) return;
      mSetConstants++;
      SetConstantsImp ();
   }
   int mRung2;

   /// <summary>Captures the current uniforms into mUniforms and returns that index</summary>
   /// We try to reuse the last-used uniforms as far as possible
   public override sealed ushort SnapUniforms () {
      // If the uniforms have not changed at all since the last time we called SnapUniforms,
      // just reuse the last one. Note that this comparison will never return true when
      // mUniforms is empty because we bump up Pix.Rung at the start of each frame, and our
      // own internal mRung value will never match for the first time this shader is used
      // in that frame. 
      if (!Lib.Set (ref mRung1, Pix.Rung)) return (ushort)(mUniforms.Count - 1);      // Fast happy path

      // Otherwise, we capture a new set of uniforms (from the Pix state like Pix.DrawColor,
      // Pix.BorderColor etc). That could also actually end up equivalent to the last used
      // uniforms, so we recycle that if OrderUniforms returns 0
      int n = mUniforms.Count;
      mUniforms.Add (SnapUniformsImp ());    // New uniform added at index n
      if (n == 0 || OrderUniforms (n - 1, n) != 0) return (ushort)n;
      mUniforms.RemoveAt (n);
      return (ushort)(n - 1);
   }
   int mRung1;

   // Private data -------------------------------------------------------------
   List<TUniform> mUniforms = [];
   List<TVertex> mData = [];
}
#endregion
