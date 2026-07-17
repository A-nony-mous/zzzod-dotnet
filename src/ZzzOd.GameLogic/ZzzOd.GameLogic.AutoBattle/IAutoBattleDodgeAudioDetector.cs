namespace ZzzOd.GameLogic.AutoBattle;

public interface IAutoBattleDodgeAudioDetector
{
	bool CheckAudio(double screenshotTime);

	void ResetBattle();

	void Start();

	void Stop();
}
