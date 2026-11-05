// Fensterpositionen wiederherstellen – Programmstart, Programmname und Assembly-Angaben
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Weitere Quelldateien im Ordner Quellcode: Speicher.cs, Ausrichten.cs, Oberflaeche.cs, Dialoge.cs, Hauptfenster.cs, Demo.cs, Update.cs.

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
  if(args.Any(x=>x=="--nach-update"))MainForm.AfterUpdate=true;
  try {Application.Run(new MainForm());}catch(Exception e){MessageBox.Show(e.Message,"Fensterpositionen",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
}
