namespace HearEqualOrNah.Audio.Capture;

using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

/// <summary>
/// WASAPI capture that requests a RAW stream (AUDCLNT_STREAMOPTIONS_RAW). Raw streams bypass the
/// capture APOs (noise suppression, automatic gain control, beamforming, voice "enhancements"),
/// which would otherwise alter a steady test tone and change the gain between the LEFT and RIGHT
/// measurements. Falls back to a normal stream when the endpoint does not support raw mode.
/// </summary>
internal sealed class RawWasapiCapture : WasapiCapture
{
    // NAudio 2.2.1 does not expose IAudioClient2 on WasapiCapture; reach the client it holds.
    private static readonly FieldInfo? CaptureClientField =
        typeof(WasapiCapture).GetField("audioClient", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ClientInterfaceField =
        typeof(AudioClient).GetField("audioClientInterface", BindingFlags.Instance | BindingFlags.NonPublic);

    public RawWasapiCapture(MMDevice device) : base(device)
    {
    }

    /// <summary>True once the stream was successfully initialised in raw mode.</summary>
    public bool IsRawMode { get; private set; }

    /// <summary>Why raw mode could not be enabled, if it was not.</summary>
    public string? RawModeFailure { get; private set; }

    // Called by WasapiCapture immediately before IAudioClient.Initialize, which is the only point at
    // which client properties may be set.
    protected override AudioClientStreamFlags GetAudioClientStreamFlags()
    {
        TryEnableRawMode();
        return base.GetAudioClientStreamFlags();
    }

    private void TryEnableRawMode()
    {
        IsRawMode = false;
        RawModeFailure = null;

        try
        {
            if (CaptureClientField?.GetValue(this) is not AudioClient client ||
                ClientInterfaceField?.GetValue(client) is not IAudioClient2Vtbl client2)
            {
                RawModeFailure = "The audio client does not support IAudioClient2.";
                return;
            }

            var properties = new AudioClientProperties
            {
                cbSize = (uint)Marshal.SizeOf<AudioClientProperties>(),
                bIsOffload = 0,
                eCategory = AudioStreamCategory.Other,
                Options = AudioClientStreamOptions.Raw
            };

            var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientProperties>());
            try
            {
                Marshal.StructureToPtr(properties, buffer, false);
                var hr = client2.SetClientProperties(buffer);
                if (hr != 0)
                {
                    RawModeFailure = $"SetClientProperties failed (0x{hr:X8}).";
                    return;
                }
                IsRawMode = true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            // Endpoint does not offer raw processing; capture continues with the default pipeline.
            RawModeFailure = ex.Message;
        }
    }
}

/// <summary>
/// Correct COM declaration of IAudioClient2. NAudio's version does not redeclare the inherited
/// IAudioClient methods, so its SetClientProperties lands on the wrong vtable slot
/// (IAudioClient::GetBufferSize, which returns AUDCLNT_E_NOT_INITIALIZED). The first twelve
/// entries below only reserve IAudioClient's slots and are never called.
/// </summary>
[ComImport]
[Guid("726778CD-F60A-4eda-82DE-E47610CD78AA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient2Vtbl
{
    [PreserveSig] int Initialize();
    [PreserveSig] int GetBufferSize();
    [PreserveSig] int GetStreamLatency();
    [PreserveSig] int GetCurrentPadding();
    [PreserveSig] int IsFormatSupported();
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDevicePeriod();
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle();
    [PreserveSig] int GetService();
    [PreserveSig] int IsOffloadCapable(AudioStreamCategory category, out int isOffloadCapable);
    [PreserveSig] int SetClientProperties(IntPtr properties);
}
