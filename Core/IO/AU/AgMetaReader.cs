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

   }

   void Check (bool condition) {
      if (!condition) throw new IOException ($"Damaged Au file (Pos:{R.Position})");
   }

   ByteStm R;
}
