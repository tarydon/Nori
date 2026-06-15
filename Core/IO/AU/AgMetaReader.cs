namespace Nori;

public class AgMetaReader : AgBase {
   public AgMetaReader (byte[] data, int offset)
      => R = new (data) { Position = offset };

   public void Parse () {
      uint read = R.ReadUInt32 (); Check (read == SIGN);
      int version = R.ReadByte (); Check (version <= VERSION);
      ReadObj (AgType.Object);
   }

   void ReadObj (AgType nominal) {
      Log ($"{mRead}] Reading object of type {nominal.Type?.NiceName () ?? "null"}");
      Push ();
      if (nominal.IsReferenceType) {
         var token = ReadToken ();
         switch (token) {
            // Null token, we just would return null here (no object)
            case EToken.Null: return;
            // An already read-in object, we would return the object from the table
            case EToken.ObjectRef: return;
            // Object of expected type to be read
            case EToken.NominalType: break;
            case EToken.DerivedType:
               // Object that follows is of some derived type, read that type ID and then
               // continue on to read an object of that type
               nominal = ReadTypeId ()!;
               Log ($"Actual type is {nominal.Type?.NiceName ()}");
               break;
            default: Fatal (); break;
         }
      }

      Pop ();
   }

   EToken ReadToken () => (EToken)R.ReadByte ();

   AgType? ReadTypeId () {
      int tid = R.ReadIntV ();
      if (tid == -1) return null;
      if (tid < mTypes.Count) return mTypes[tid];
      if (tid > mTypes.Count) Fatal ();

      AgType? au;
      var token = ReadToken ();
      Push (); 
      switch (token) {
         case EToken.ClassType: 
            var (name, version) = (R.ReadString ()!, R.ReadIntV ());
            au = new (name, version); mTypes.Add (au);
            au.Base = ReadTypeId ();
            Log ($"Type {tid}] class {name} : {au.Base?.Type?.NiceName ()}");
            Push ();
            int cFields = R.ReadIntV ();
            for (int i = 0; i < cFields; i++) {
               string name = R.ReadString ()!, altName = R.ReadString ()!;

            }

            Pop ();
            break;

         default: throw new BadCaseException (token);
      }
      Pop ();
      return au; 
   }

   void Check (bool condition) {
      if (!condition) throw new IOException ($"Damaged Au file (Pos:{R.Position})");
   }

   void Fatal () => Check (false);

   void Push () => mLevel++;
   void Pop () => mLevel--;
   int mLevel = 0, mRead = 0; 

   void Log (string message) {
      Console.WriteLine (new string (' ', mLevel * 2) + message);
   }

   ByteStm R;
   List<AgType> mTypes = [];
}
