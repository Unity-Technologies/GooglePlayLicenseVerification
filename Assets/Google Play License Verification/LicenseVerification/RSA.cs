using System.Security.Cryptography;

class RSA
{
	public static void SimpleParseASN1(string publicKey, ref byte[] modulus, ref byte[] exponent)
	{
		byte[] publicKey64 = System.Convert.FromBase64String(publicKey);
		
		// The ASN1 structure for the public key looks like this:
		//
		//     SubjectPublicKeyInfo ::= SEQUENCE {
		//        algorithm AlgorithmIdentifier,
		//        publicKey BIT STRING }
		//
		// Where the BIT STRING is a SEQUENCE of 2 INTEGERs (the modulus and the exponent)
		
		System.Type ASN1 = System.Type.GetType("Mono.Security.ASN1");
		System.Reflection.ConstructorInfo Ctor = ASN1.GetConstructor(new System.Type[] { typeof(byte[]) });
		System.Reflection.PropertyInfo Value = ASN1.GetProperty("Value");
		System.Reflection.PropertyInfo Item = ASN1.GetProperty("Item");
		
		object asn = Ctor.Invoke(new object[] { publicKey64 } );
		object bits = Item.GetValue(asn, new object[] { 1 });

		byte[] value = (byte[])Value.GetValue(bits, null);

		byte[] seq = new byte[value.Length-1];
		System.Array.Copy(value, 1, seq, 0, value.Length-1);

		asn = Ctor.Invoke(new object[] { seq } );
		
		object asn0 = Item.GetValue(asn, new object[]{ 0 });
		object asn1 = Item.GetValue(asn, new object[]{ 1 });

		modulus = (byte[])Value.GetValue(asn0, null);
		exponent = (byte[])Value.GetValue(asn1, null);
		
		// non-reflected version
	//	Mono.Security.ASN1 asn = new Mono.Security.ASN1(publicKey64);
	//	Mono.Security.ASN1 bits = asn[1];
	//	byte[] seq = new byte[bits.Length-1];
	//	System.Array.Copy(bits.Value, 1, seq, 0, bits.Length-1);
	//	asn = new Mono.Security.ASN1(seq);
	//	modulus = asn[0].Value;
	//	exponent = asn[1].Value;
	}
}
