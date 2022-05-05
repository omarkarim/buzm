using System;

namespace Buzm.Register
{
	/// <summary>The list of reponses that a Registry server
	/// might return to a RegistryAction request. Explicit values 
	/// are used for backward compatibility. A None value implies
	/// that no Registry has handled the request as yet. </summary>
	public enum RegistryResult : int
	{
		None = 0,
		Success = 1,
		UserError = 2,
		ClientError = 3,
		RegistryError = 4,
		UnknownRequest = 5,
		UnsupportedRequest = 6,
		UnknownError = 7,
		NetworkError = 8,
		ServerError = 9,
		ServerBusy = 10,
		Cancelled = 11,
		Warning = 12
	}
}