namespace ZzzOd.GameLogic.Backend;

internal static class BackendRunStateExtensions
{
	public static string ToSchemaValue(this BackendRunState state)
	{
		if (1 == 0)
		{
		}
		string result = state switch
		{
			BackendRunState.Idle => "idle", 
			BackendRunState.Running => "running", 
			BackendRunState.Success => "success", 
			BackendRunState.Failed => "failed", 
			BackendRunState.Stopped => "stopped", 
			_ => "failed", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
