using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

namespace Buzm.Network.Sockets
{
	public class Serialization
	{
		private static byte[] m_EncryptKey;
		private static byte[] m_EncryptIV;

		static Serialization()
		{
			// Hardcoded initialization vector and encryption key
			// TODO: Use asymmetric encryption to establish keys in handshake
			m_EncryptIV	 = new byte[]{ 0xCE, 0x24, 0x26, 0xFA, 0x36, 0xE3, 0x3B, 0xD5, 0x4D, 0xC4, 0xD5, 0xE6, 0x91, 0xA7, 0xE5, 0x52 };
			m_EncryptKey = new byte[]{ 0x11, 0xB0, 0xB1, 0x93, 0x61, 0x1D, 0xCA, 0x1C, 0xBE, 0xF6, 0x45, 0x30, 0xCD, 0x1B, 0x7B, 0xC2 };
		}

		public static object ReadObject( Stream source )
		{
			// Extract object length
			int length = ReadLength( source );
			byte[] buffer = new byte[length];
						
			// Wrap Rijndael encryption around source stream
			RijndaelManaged cryptoAlg = new RijndaelManaged();
			CryptoStream cryptoBuffer = new CryptoStream( source, 
											cryptoAlg.CreateDecryptor(m_EncryptKey, m_EncryptIV),   
											CryptoStreamMode.Read );


			// Decrypt object data to buffer
			// TODO: truncate source to block size
			cryptoBuffer.Read( buffer, 0, length );
			MemoryStream memStream = new MemoryStream( buffer );

			// Deserialize object from memory stream
			BinaryFormatter formatter = new BinaryFormatter();
			object obj = formatter.Deserialize( memStream );

			memStream.Close();
			return obj;
		}

		public static void WriteObject( object obj, Stream target )
		{	
			// Wrap Rijndael encryption around memory stream
			MemoryStream buffer			= new MemoryStream();
			RijndaelManaged cryptoAlg	= new RijndaelManaged();
			CryptoStream cryptoBuffer	= new CryptoStream( buffer, 
											cryptoAlg.CreateEncryptor(m_EncryptKey, m_EncryptIV),   
											CryptoStreamMode.Write );

			// Serialize object to encrypted buffer
			BinaryFormatter formatter = new BinaryFormatter();
			formatter.Serialize( cryptoBuffer, obj );

			// Finalize encryption
			cryptoBuffer.FlushFinalBlock();
			buffer.Position = 0;

			// Write length unencrypted
			int length = (int)buffer.Length;
			WriteLength( length, target );

			// Write object encrypted
			buffer.WriteTo( target );
			cryptoBuffer.Close();
			buffer.Close();
		}

		public static void DecryptStream( Stream source )
		{

		}

		public static void EncryptStream( object obj, Stream target )
		{
			
		}

		public static int ReadLength( Stream source )
		{
			byte[] buffer = new byte[4];
			source.Read( buffer, 0, 4 );
			return ByteArrayToInt( buffer );
		}

		public static void WriteLength( int length, Stream target )
		{
			byte[] buffer = IntToByteArray( length );
			target.Write( buffer, 0, 4 );
		}

		private static int ByteArrayToInt( byte[] input )
		{
			int output = 0;
			output += input[0] * (int)Math.Pow(256, 0);
			output += input[1] * (int)Math.Pow(256, 1);
			output += input[2] * (int)Math.Pow(256, 2);
			output += input[3] * (int)Math.Pow(256, 3);
			return output;
		}

		private static byte[] IntToByteArray( int input )
		{
			byte[] output = new byte[4];
			output[0] = (byte)((input & 0xFFL) / Math.Pow(256, 0));
			output[1] = (byte)((input & 0xFF00L) / Math.Pow(256, 1));
			output[2] = (byte)((input & 0xFF0000L) / Math.Pow(256, 2));
			output[3] = (byte)((input & 0xFF000000L) / Math.Pow(256, 3));
			return output;
		}

		#region NUnit Automated Test Cases

		[TestFixture] public class SerializationTest
		{
			[SetUp] public void SetUp(){ }
			[TearDown] public void TearDown(){ }

			[Test] public void RoundtripIntToByteArray()
			{
				// Typical case
				int input = 721853;
				byte[] bytes = IntToByteArray( input );
				int output = ByteArrayToInt( bytes );
				
				Assertion.AssertEquals( "Incorrect first byte.", bytes[0], 189 );
				Assertion.AssertEquals( "Incorrect second byte.", bytes[1], 3 );
				Assertion.AssertEquals( "Incorrect third byte.", bytes[2], 11 );
				Assertion.AssertEquals( "Incorrect fourth byte.", bytes[3], 0 );
				Assertion.AssertEquals( "Integer to Bytes conversion failed.", output, input );
				
				// Worst case
				int maxInput = 2147483647;
				byte[] maxBytes = IntToByteArray( maxInput );
				int maxOutput = ByteArrayToInt( maxBytes );
				Assertion.AssertEquals( "Worst case Integer to Bytes conversion failed.", maxOutput, maxInput );

			}

			[Test] public void ObjectSerialization()
			{
				//Create test data
				TestObject inObj = new TestObject( 3, "hello!" );
				MemoryStream stream = new MemoryStream();

				//Pass object through serialization functions;
				WriteObject( inObj, stream );
				stream.Position = 0;
				TestObject outObj = (TestObject)ReadObject( stream );

				//Compare input and output object data
				Assertion.AssertEquals( "Integer class vars did not match.", outObj.Num, inObj.Num );
                Assertion.AssertEquals( "String class vars did not match.", outObj.Str, inObj.Str );
			}

			[Test] public void RandomKeysTest()
			{
				// create new encryption obj to generate keys
				RijndaelManaged rijndael = new RijndaelManaged();
				rijndael.KeySize = rijndael.LegalKeySizes[0].MinSize;
				
				rijndael.GenerateKey(); // generate random encryption key 
				rijndael.GenerateIV(); // and initialization vector

				// set global key/iv to generated key/iv
				Serialization.m_EncryptKey = rijndael.Key;
				Serialization.m_EncryptIV = rijndael.IV;

				string ivString = String.Empty; 
				string keyString = String.Empty;
				
				int byteCount = rijndael.KeySize / 8; 
				for( int i=0; i < byteCount; i++ ) // build hex byte strings
				{
					keyString += "0x" + Serialization.m_EncryptKey[i].ToString( "X2" ) + ", ";
					ivString += "0x" + Serialization.m_EncryptIV[i].ToString( "X2" ) + ", ";
				}

				// create test data
				TestObject inObj = new TestObject( 3, "hello!" );
				MemoryStream stream = new MemoryStream();

				// pass object through serialization functions
				WriteObject( inObj, stream );
				stream.Position = 0;
				TestObject outObj = (TestObject)ReadObject( stream );

				// compare input and output object data
				Assert.AreEqual( inObj.Num, outObj.Num, "Integer vars did not match with key: " + keyString + " and IV: " + ivString );
				Assert.AreEqual( inObj.Str, outObj.Str, "String vars did not match with key: " + keyString + " and IV: " + ivString );
			}

			[Serializable] private class TestObject 
			{
				public int Num;
				public string Str;

				public TestObject( int num, string str )
				{	
					Num = num;
					Str = str;
				}
			}
		}

		#endregion

	}
}