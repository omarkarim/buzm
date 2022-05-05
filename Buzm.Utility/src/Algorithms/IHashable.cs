namespace Buzm.Utility.Algorithms
{
	public interface IHashable
	{
		byte[] Bytes { get; }
		byte[] Hash { get; set; }
	}
}
