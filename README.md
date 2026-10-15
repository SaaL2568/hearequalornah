# hearequalornah

**Is one side of your IEMs louder than the other? Find out in two minutes using your laptop's microphone.**

hearequalornah is a free Windows app that checks the left/right balance of in-ear monitors (IEMs), earbuds, and headphones. It plays the same test tone through each side, measures how loud each one is at your computer's microphone, and tells you the difference in decibels (dB). If there is an imbalance, it can correct it for you with the Windows left/right balance, and you can run the test again to confirm.

No account, no internet connection, no install.

> [!NOTE]
> This is not a hearing test. It measures your earphones, not your ears.

---

## Contents

- [What you need](#what-you-need)
- [Download](#download)
- [Quick start](#quick-start)
- [Getting an accurate result](#getting-an-accurate-result)
- [Understanding your result](#understanding-your-result)
- [Fixing an imbalance](#fixing-an-imbalance)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Privacy](#privacy)
- [How it works](#how-it-works)
- [Building from source](#building-from-source)

---

## What you need

- A Windows 10 or Windows 11 PC (64-bit)
- A built-in or external microphone (a laptop mic works well)
- The earphones you want to test, plugged in to that PC (3.5 mm, USB-C dongle/DAC, or USB)
- A quiet room

Wired connections give the most reliable results. Bluetooth earphones usually work, but some switch to a low-quality mono mode when their own microphone is in use, so pick your computer's microphone, not the headset's.

## Download

1. Go to the [Releases](../../releases) page and download `hearequalornah.exe` from the latest release.
2. Double-click it. There is nothing to install.

> [!TIP]
> **"Windows protected your PC" warning:** the app is not code-signed, so Windows SmartScreen may warn you the first time. Click **More info**, then **Run anyway**. If you would rather not do that, you can [build it yourself](#building-from-source) from this source code.

To remove the app, delete `hearequalornah.exe` and the folder `%LOCALAPPDATA%\hearequalornah`.

## Quick start

> [!WARNING]
> Take the earphones out of your ears before testing. The test plays a beep at fairly high volume through the earphone you are holding at the microphone.

1. **Take the earphones out of your ears.** You will hold them at the microphone during the test, not wear them.
2. Open the app and choose your **Microphone** and your earphones as the **Output device**.
3. **Find your microphone opening.** Laptop microphones are tiny pinholes, usually in the top bezel near the webcam (there are often two). Tap or blow lightly along the bezel while watching the **MICROPHONE SENSITIVITY (LIVE)** bar. The opening is where the bar jumps the most.
4. Set your Windows volume fairly high (around 60-80%). The test compares the two sides, so the exact volume does not change the result. Because you are not wearing the earphones, this is safe.
5. Click **START TEST** and stay quiet for a moment while it measures the background noise.
6. Point the **left** earphone's nozzle straight at the microphone opening, a few millimetres away, and click **MEASURE LEFT**. Keep it still while it plays a few short beeps (about 10 seconds).
7. Put the **right** earphone in **exactly the same spot and angle**, then click **MEASURE RIGHT**.
8. Read your result.

## Getting an accurate result

Placement matters more than anything else. Moving the earphone 1 mm closer or tilting it slightly can change the reading by 1 dB or more, so the goal is to put both sides in exactly the same place.

- **Don't hand-hold if you can avoid it.** Rest the earphone on a book, a phone, or a folded cloth at the right height, or tape a small guide next to the microphone. Then swap left and right into the same position.
- **Point the nozzle (sound outlet), not the body,** straight at the microphone opening.
- **Get close.** IEMs are designed to seal inside your ear canal and are very quiet in open air, so a few millimetres from the opening works much better than a few centimetres.
- **Remove ear tips if they differ.** Use the same tip size on both sides, or remove both, so the tips don't change the result.
- **Clean the nozzles.** Earwax or a clogged filter on one side is a common real cause of imbalance, but measure first, then clean, then measure again to see the difference.
- **Check repeatability.** Run the test twice without changing anything. If the two results differ by more than about 0.5 dB, your placement is not consistent yet.
- **Turn off audio "enhancements"** for the output device if you use them (spatial sound, loudness equalization, Dolby/DTS/vendor EQ).

## Understanding your result

The **difference** is how much louder one side is than the other at 1 kHz, the frequency range where your hearing is most sensitive.

| Difference | Rating | What it means |
|---|---|---|
| 0 - 1 dB | Excellent balance | Generally not noticeable. Nothing to fix. |
| 1 - 2 dB | Good balance | Most people won't notice it; sensitive listeners might, especially with centred vocals. |
| 2 - 3 dB | Slight imbalance | Centre images (vocals) may sound shifted to one side. A correction may help. |
| 3 - 6 dB | Significant imbalance | Usually audible. Check placement, clean the nozzles, then correct or contact the manufacturer. |
| over 6 dB | Strong imbalance | Very likely a real problem: blocked nozzle or filter, damaged driver, or a cable/connector fault. |

Before deciding your earphones are faulty, repeat the test with careful placement. If one side is consistently quieter across several careful runs, and especially if cleaning it doesn't help, the imbalance is real.

Each side also gets a quality label. **Good** means all attempts agreed. **Unstable** means one attempt was thrown out because it was too different from the others, usually because the earphone moved.

## Fixing an imbalance

If your output device supports per-channel volume (most wired and USB devices do), the app shows a **recommended balance**, for example "right 1.8 dB below left", next to your current Windows balance.

You can fix it in either of two ways:

- **Let the app do it:** click **APPLY CORRECTION**. The app never turns anything up. It keeps the louder side where it is and lowers the other one to match, so your overall volume does not increase.
- **Do it yourself:** open Windows **Settings > System > Sound**, select your output device, and adjust the **Left channel / Right channel** sliders. The app's **Now** readout updates every second, so you can watch it reach the **Target**.

Then **run the test again** to confirm the difference is close to 0.

- **UNDO LAST CORRECTION** puts back the balance you had before.
- **RESET TO EQUAL** sets both sides to the same level (at the louder side's current level, so it never jumps to full volume).

Notes:

- The Windows balance is per device, so it only applies to that output device (for example, your USB-C dongle).
- It fixes the loudness difference only. It cannot fix a damaged driver that sounds different, not just quieter.
- The app only changes your Windows balance when you click one of these buttons.

## Troubleshooting

| What you see | What to try |
|---|---|
| **"The test tone was not detected"** | Make sure the correct **Output device** is selected and you can hear the beep. Move the earphone nozzle closer to the microphone opening (use the live meter to find it). Turn the volume up. If only one side ever fails, listen to that side with music: it may really be faulty. |
| **"Could not capture enough clean measurements because of background noise"** | Make the room quieter (fans, AC, TV, typing), or move the earphone closer to the microphone so the tone is louder. |
| **"The measurement varied too much between attempts"** | The earphone moved between beeps. Rest it on something stable instead of holding it. |
| **"Microphone input is clipping"** | It's too loud at the microphone. Lower the volume a little or move the earphone slightly further away. |
| **"The microphone is delivering complete silence"** | Your microphone is muted or blocked. Check **Settings > Privacy & security > Microphone** and allow desktop apps to use it. |
| **"The selected output device is mono"** | Left and right can't be tested separately on this device. For Bluetooth headsets, choose your computer's microphone instead of the headset's. |
| **Balance section says the device doesn't support channel balance** | Testing still works; only the automatic correction is unavailable for that device. |
| **The microphone level bar barely moves** | Pick a different microphone in the list and tap near it to see which one responds. |

If something still isn't working, open an [issue](../../issues) and attach the log file from `%LOCALAPPDATA%\hearequalornah\app.log` (paste that path into the File Explorer address bar). It records what the microphone heard on each attempt, which usually shows the problem straight away.

## FAQ

**Will this damage my earphones or my hearing?**
The test tone is a moderate-level beep, and you are not wearing the earphones during the test. Don't put them in your ears while testing at high volume.

**Do I need a measurement microphone?**
No. Because both sides are measured with the same microphone at the same frequency and in the same position, the microphone's own characteristics cancel out. Only placement needs to be consistent.

**Why does the app show lower dB levels than other apps?**
It opens the microphone in Windows "raw" mode, which turns off noise suppression and automatic gain so they can't change the result between left and right. Raw mode also removes the microphone's software boost, so the absolute level is lower. Only the difference between left and right matters.

**Why only 1 kHz?**
1 kHz is the standard reference frequency for channel matching, and it sits where hearing is most sensitive. An imbalance can vary across frequencies, but a difference at 1 kHz is the one you are most likely to hear as a shifted centre image.

**My result changes every time I run it.**
That is almost always placement. See [Getting an accurate result](#getting-an-accurate-result).

**Does it work with headphones (over-ear / on-ear)?**
Yes. Hold each ear cup flat over the microphone in the same way for both sides.

**Does it work on macOS or Linux?**
No, it uses Windows audio APIs and is Windows only.

## Privacy

- The app works completely offline and never sends anything over the internet.
- Microphone audio is analysed in memory and never saved or recorded.
- The app stores only your device choices, past test results, and a diagnostic log, all under `%LOCALAPPDATA%\hearequalornah` on your PC.

## How it works

For the technically curious:

- **Test signal:** a 1 kHz sine at -6 dBFS, 2.5 s long with 50 ms fades, identical for both channels and played on one channel at a time.
- **Playback:** the output stream is opened once per test and kept running (playing silence between beeps), so USB and Bluetooth devices don't mute, reset, or change latency between attempts.
- **Capture:** 48 kHz mono in Windows RAW mode where supported, bypassing capture processing (noise suppression, AGC, voice enhancement) that would alter a steady tone. If raw mode isn't available, the app falls back to the normal stream and logs a warning.
- **Timing:** each attempt records a 0.3 s silent pre-roll, then the whole tone. Only a steady 1.5 s window starting 0.5 s after the tone begins is analysed, so output latency and fades never affect the result.
- **Level and noise:** the tone level is measured with a Goertzel filter at 1 kHz. The noise floor is measured in the same window and bandwidth at nearby frequencies chosen to avoid 50/60 Hz mains hum harmonics.
- **Quality gates:** these are relative to the noise floor, so they work with any microphone gain.
  - Below 10 dB signal-to-noise ratio the tone counts as not detected.
  - Below 25 dB the attempt is retried, which keeps the error of one attempt within about ±0.5 dB.
  - Clipping and pure digital silence are detected separately.
- **Result:** at least three clean attempts per side (up to six tries). Outliers more than 1.5 dB from the median are discarded, and the result is the median of the rest.
- **Correction:** the Windows per-channel levels are read and written in dB through `IAudioEndpointVolume`. The percentage sliders are not linear, so working in dB is what makes the correction match the measurement. Corrections build on any balance already set, are applied relative to your current volume when you click APPLY, and never raise the louder channel.

## Building from source

Requirements: Windows 10/11 x64 and the [.NET 8 SDK](https://aka.ms/dotnet-download).

```powershell
# build
dotnet build src\HearEqualOrNah.sln

# run
dotnet run --project src\HearEqualOrNah.UI

# test
dotnet test src\HearEqualOrNah.sln

# publish a single self-contained exe to publish\hearequalornah.exe
dotnet publish src\HearEqualOrNah.UI\HearEqualOrNah.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Don't enable trimming: WPF doesn't support it, and the raw-microphone code accesses NAudio internals by reflection.

The xUnit tests cover signal analysis, measurement-engine logic, classification, correction math, and the playback stream lifecycle. Hardware behaviour (actual playback, capture, and Windows balance changes) is validated manually.

Project layout:

- `src/HearEqualOrNah.Core` - models, measurement engine, signal generation and analysis, classification, correction math, history, settings
- `src/HearEqualOrNah.Audio` - NAudio playback, raw-mode microphone capture, device enumeration
- `src/HearEqualOrNah.WindowsAudio` - Windows endpoint volume and channel balance
- `src/HearEqualOrNah.UI` - WPF interface (MVVM) and file logging
- `src/HearEqualOrNah.Tests` - xUnit tests

## Not a medical test

hearequalornah measures the acoustic output of your earphones under your test setup. It does not test your hearing, and its results say nothing about the listener's hearing. If you notice that one ear hears less than the other with several different earphones, see a hearing professional.

## License

[MIT](LICENSE)
