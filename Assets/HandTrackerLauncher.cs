using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// Lança o hand_tracker_server.exe automaticamente ao iniciar o jogo
/// e o encerra quando a aplicação fechar.
/// Adicione este componente a qualquer GameObject persistente na cena.
public class HandTrackerLauncher : MonoBehaviour
{
    private Process _trackerProcess;

    private void Awake()
    {
        TryLaunchHandTracker();
    }

    private void TryLaunchHandTracker()
    {
        string trackerDir = Path.GetFullPath(Path.Combine(
            Application.streamingAssetsPath, "hand_tracker"));
        string exePath  = Path.Combine(trackerDir, "hand_tracker_server.exe");
        string batPath  = Path.Combine(trackerDir, "start_tracker.bat");
        string logPath  = Path.Combine(Application.persistentDataPath, "hand_tracker.log");

        var log = new System.Text.StringBuilder();
        log.AppendLine($"TrackerDir : {trackerDir}");
        log.AppendLine($"ExeFound   : {File.Exists(exePath)}");
        log.AppendLine($"BatFound   : {File.Exists(batPath)}");

        if (!File.Exists(exePath))
        {
            log.AppendLine("RESULTADO: exe nao encontrado.");
            File.WriteAllText(logPath, log.ToString());
            UnityEngine.Debug.Log("[HandTrackerLauncher] hand_tracker_server.exe nao encontrado. " +
                                  "Copie a pasta hand_tracker para StreamingAssets.");
            return;
        }

        foreach (var old in Process.GetProcessesByName("hand_tracker_server"))
        {
            try { old.Kill(); old.WaitForExit(1000); old.Dispose(); } catch { }
        }

        try
        {
            ProcessStartInfo psi;

            if (File.Exists(batPath))
            {
                psi = new ProcessStartInfo
                {
                    FileName         = "cmd.exe",
                    Arguments        = $"/C \"{batPath}\"",
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    WorkingDirectory = trackerDir
                };
                log.AppendLine("Metodo: cmd.exe + start_tracker.bat");
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName         = exePath,
                    Arguments        = "--no-window",
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    WorkingDirectory = trackerDir
                };
                log.AppendLine("Metodo: direto (bat nao encontrado)");
            }

            _trackerProcess = Process.Start(psi);
            log.AppendLine($"RESULTADO: iniciado PID={_trackerProcess?.Id}");
            UnityEngine.Debug.Log($"[HandTrackerLauncher] Hand Tracker iniciado (PID {_trackerProcess?.Id})");
        }
        catch (Exception e)
        {
            log.AppendLine($"RESULTADO: ERRO — {e.Message}");
            UnityEngine.Debug.LogWarning($"[HandTrackerLauncher] Falha ao iniciar Hand Tracker: {e.Message}");
        }

        File.WriteAllText(logPath, log.ToString());
    }

    private void OnApplicationQuit()
    {
        try
        {
            if (_trackerProcess != null && !_trackerProcess.HasExited)
            {
                _trackerProcess.Kill();
                _trackerProcess.WaitForExit(2000);
                _trackerProcess.Dispose();
            }
        }
        catch { }

        foreach (var p in Process.GetProcessesByName("hand_tracker_server"))
        {
            try { p.Kill(); p.Dispose(); } catch { }
        }
    }
}
