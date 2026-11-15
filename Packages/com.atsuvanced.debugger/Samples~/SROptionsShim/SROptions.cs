using System.ComponentModel;
using UnityEngine;

/// <summary>
/// SRDebugger compatible SROptions.
/// This file must live in the same assembly as your 'partial class SROptions' code (usually Assembly-CSharp),
/// which is why it is shipped as a sample instead of inside the package.
/// </summary>
public partial class SROptions : INotifyPropertyChanged
{
    private static SROptions _current;

    public static SROptions Current => _current ??= new SROptions();

    public event PropertyChangedEventHandler PropertyChanged;

    public void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SRDebug.Instance.AddOptionContainer(Current);
    }
}
