namespace Buzm.Utility.Algorithms
{
	public class HashTreeNode : IHashable
	{
		private byte[] m_Hash; // simple hash store
		public HashTreeNode( byte[] hash ) { m_Hash = hash; }

		public byte[] Hash { get { return m_Hash; } set { m_Hash = value; } }
		public byte[] Bytes { get { return null; } }
	}
}
