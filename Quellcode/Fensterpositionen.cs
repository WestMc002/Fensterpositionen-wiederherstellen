// Fensterpositionen wiederherstellen – Programmstart, Programmname und Assembly-Angaben
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Weitere Quelldateien im Ordner Quellcode: Speicher.cs, Ausrichten.cs, Oberflaeche.cs, Dialoge.cs, Hauptfenster.cs, Demo.cs, Update.cs, Meldungen.cs.

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Drawing.Drawing2D;

[assembly: AssemblyTitle(Fensterpositionen.AppInfo.Product)]
[assembly: AssemblyProduct(Fensterpositionen.AppInfo.Product)]
[assembly: AssemblyDescription("Fenster sichern, ausrichten und wiederherstellen")]
[assembly: AssemblyCopyright("© 2026 Günther Schoder")]
// Versionsnummer: wird von Erstellen.bat in VersionInfo.cs erzeugt.

namespace Fensterpositionen {
// Version (Hauptversion.Nebenversion.Jahr.MonatTag) steht in VersionInfo.cs, erzeugt von Erstellen.bat.
public static partial class AppInfo {
 public const string Product="Fensterpositionen";
}
static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Length>0 && args[0]=="--self-test"){SelfTest.Run();return;}
  try {Native.SetProcessDPIAware();} catch {}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Ui.Init();
  if(args.Length>0 && args[0]=="--screenshots"){
   // Screenshots für GitHub aus Beispieldaten (Demo.cs); Ziel standardmäßig der Ordner Screenshots neben der EXE.
   string dir=args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Screenshots");
   if(!Demo.RunScreenshots(dir))Environment.ExitCode=1;
   return;
  }
  // Neustart durch das Update-Skript: Ergebnis des Updates ins Protokoll schreiben.
  // "--nach-update [<Kennung>] [/tray]": Kennung des Versuchs (32 Hex-Zeichen, ab 2.68; das Ergebnis steht in einer
  // eigenen Datei je Versuch). /tray wird ignoriert (kein Infobereich).
  int after=Array.FindIndex(args,x=>String.Equals(x,"--nach-update",StringComparison.OrdinalIgnoreCase));
  if(after>=0) {
   MainForm.AfterUpdate=true;
   if(after+1<args.Length&&Updater.IsAttemptId(args[after+1]))MainForm.UpdateAttemptId=args[after+1];
  }
  // "--aktualisieren <Prozess-ID>": Fensterpositionen lief mit Administratorrechten und hat sich für das Update mit
  // normalen Rechten neu gestartet (Updater.RestartUnelevated) – nach dem Ende der alten Instanz ohne weitere Rückfrage
  // aktualisieren. Löst nie einen weiteren Neustart aus.
  int now=Array.FindIndex(args,x=>String.Equals(x,"--aktualisieren",StringComparison.OrdinalIgnoreCase));
  if(now>=0) {
   MainForm.InstallNow=true;
   int old;
   if(now+1<args.Length&&Int32.TryParse(args[now+1],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out old))MainForm.InstallNowWaitFor=old;
  }
  // Verknüpfung "Fenster wiederherstellen": --wiederherstellen [Anordnung] stellt minimiert wieder her und beendet sich bei Erfolg.
  int restore=Array.IndexOf(args,"--wiederherstellen");
  if(restore>=0) {
   MainForm.AutoRestore=true;
   if(restore+1<args.Length&&!args[restore+1].StartsWith("--"))Store.Override=args[restore+1];
  }
  try {Application.Run(new MainForm());}catch(Exception e){MessageBox.Show(e.Message,"Fensterpositionen",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
}
