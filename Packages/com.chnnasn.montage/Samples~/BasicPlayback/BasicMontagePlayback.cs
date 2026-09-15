using ChnnAsn.Montage;
using UnityEngine;

public sealed class BasicMontagePlayback : MonoBehaviour
{
    [SerializeField] private MontagePlayer player;
    [SerializeField] private MontageAsset montage;

    private MontageHandle handle;

    public void Play()
    {
        handle = player.Play(montage, MontagePlayOptions.Default);
    }

    public void Stop()
    {
        handle.Stop(0.15f);
    }

    public void JumpToSection(string section)
    {
        handle.JumpToSection(section);
    }
}
