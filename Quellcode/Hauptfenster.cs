// Fensterpositionen wiederherstellen – Hauptfenster mit Programmliste, Aktionen und Protokoll
// Copyright (C) 2026 Günther Schoder · https://www.westmc002.de
// Lizenz: GNU General Public License v3.0 oder später (siehe LICENSE)
// SPDX-License-Identifier: GPL-3.0-or-later

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

namespace Fensterpositionen {
public class MainForm:Form {
 // Demo-Ansicht für die Screenshots (--screenshots): ohne eigene Programme und ohne Datenübernahme.
 public static bool DemoMode;
 ProgramList list=new ProgramList();
 ModernButton restore,save,all,folder,admin,addProgram;
 // Ausrichten: je eine Schaltfläche pro Kante und "Automatisch" (Regeln je Programm), Abstand frei wählbar.
 ModernButton alignTop,alignBottom,alignLeft,alignRight,alignAuto,alignRules;
 readonly TextBox gapBox=new TextBox();
 readonly ToolTip alignTips=new ToolTip();
 const int DefaultGap=18;
 TableLayoutPanel layout;
 readonly ContextMenuStrip rowMenu=new ContextMenuStrip();
 TextBox log=new TextBox();
 InfoBar hint=new InfoBar();
 ActivityBar activity=new ActivityBar();
 FooterPanel footer=new FooterPanel();
 Bitmap signature;
 Label selectionInfo=new Label();
 List<Entry> entries=new List<Entry>();
 // Alle fest eingebauten Programme in ihrer Reihenfolge (auch ausgeblendete und nicht installierte).
 readonly List<Entry> builtIns=new List<Entry>();
 Dictionary<string,object> profiles;
 bool busy;
 public MainForm() {
  // Screenshots (Demo-Ansicht): kein Administrator-Hinweis, auch wenn Erstellen.bat erhöht läuft.
  bool elevated=!DemoMode&&IsElevated();
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); Text=AppInfo.Product+" "+AppInfo.Version+(elevated?" (Administrator)":"");
  AutoScaleMode=AutoScaleMode.None; Font=Ui.UiFont; BackColor=Ui.Bg; ForeColor=Ui.TextColor;
  StartPosition=FormStartPosition.CenterScreen;
  // Einstellungen aus dem alten Ordner (bis Version 2.12) einmalig übernehmen.
  string migrated=DemoMode?null:Store.MigrateOldFolder();
  Setup();profiles=Store.Load();
  builtIns.AddRange(entries);
  string skipped=null,hiddenNames=null;
  if(!DemoMode) {
   // Fest eingebaute Programme, die hier nicht installiert sind, erscheinen nicht; selbst ausgeblendete ebenso.
   var hidden=Store.HiddenIds(profiles);
   var missing=entries.Where(x=>!Installed(x)).ToList();
   var off=entries.Where(x=>hidden.Contains(x.Id)&&Installed(x)).ToList();
   if(missing.Count>0)skipped=String.Join(", ",missing.Select(x=>x.Name).ToArray());
   if(off.Count>0)hiddenNames=String.Join(", ",off.Select(x=>x.Name).ToArray());
   entries.RemoveAll(x=>missing.Contains(x)||off.Contains(x));
   entries.AddRange(Store.CustomEntries(profiles));
  }
  restore=new ModernButton("Wiederherstellen",ButtonKind.Primary,"");
  save=new ModernButton("Positionen speichern",ButtonKind.Secondary,"");
  alignTop=new ModernButton("Oben",ButtonKind.Secondary,"\uE74A");
  alignBottom=new ModernButton("Unten",ButtonKind.Secondary,"\uE74B");
  alignLeft=new ModernButton("Links",ButtonKind.Secondary,"\uE72B");
  alignRight=new ModernButton("Rechts",ButtonKind.Secondary,"\uE72A");
  alignAuto=new ModernButton("Automatisch",ButtonKind.Secondary,"\uE740");
  folder=new ModernButton("Sicherungen öffnen",ButtonKind.Secondary,"");
  all=new ModernButton("Alle auswählen",ButtonKind.Ghost,null);
  all.Height=Ui.Px(32);all.Margin=new Padding(0);folder.Margin=new Padding(0);
  addProgram=new ModernButton("Programm hinzufügen",ButtonKind.Ghost,"\uE710");
  addProgram.Height=Ui.Px(32);addProgram.Margin=new Padding(0);

  var header=new HeaderPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,Ui.Px(12)),Title=AppInfo.Product,Subtitle="Fenster sichern, ausrichten und wiederherstellen",Badge="Version "+AppInfo.Version};
  header.Logo=LoadLogo(Ui.Px(48));
  signature=LoadSignature();
  header.BadgeClick+=(s,e)=>ShowInfo();
  footer.Dock=DockStyle.Fill;footer.Margin=new Padding(0);footer.Logo=signature;
  // Globale Richtlinie: links "© <Jahr> · <Programmname>".
  footer.Caption="© "+DateTime.Now.Year+" · "+AppInfo.Product;
  header.Controls.Add(folder);
  if(!elevated) {
   // Manche Programme (z.B. Easy2Sync) laufen mit Administratorrechten; ihre Fenster lassen sich nur mit gleichen Rechten verschieben.
   admin=new ModernButton("Als Administrator",ButtonKind.Secondary,"\uE7EF");
   admin.Margin=new Padding(0);
   admin.Click+=(s,e)=>RestartElevated();
   header.Controls.Add(admin);
  }
  header.Resize+=(s,e)=>{
   folder.Location=new Point(header.Width-folder.Width,(header.Height-folder.Height)/2);
   if(admin!=null)admin.Location=new Point(folder.Left-Ui.Px(8)-admin.Width,(header.Height-admin.Height)/2);
  };

  activity.Dock=DockStyle.Fill;activity.Margin=new Padding(0,0,0,Ui.Px(14));

  var programsCard=new Card{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,Ui.Px(14)),Padding=new Padding(Ui.Px(8))};
  var programsHead=new Panel{Dock=DockStyle.Top,Height=Ui.Px(44),BackColor=Ui.Surface};
  var programsTitle=new Label{Text="Programme",AutoSize=true,Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface};
  selectionInfo.AutoSize=true;selectionInfo.Font=Ui.SmallFont;selectionInfo.ForeColor=Ui.Muted;selectionInfo.Tag="muted";selectionInfo.BackColor=Ui.Surface;
  programsHead.Controls.Add(programsTitle);programsHead.Controls.Add(selectionInfo);programsHead.Controls.Add(all);programsHead.Controls.Add(addProgram);
  EventHandler layoutHead=(s,e)=>{
   programsTitle.Location=new Point(Ui.Px(16),(programsHead.Height-programsTitle.Height)/2);
   selectionInfo.Location=new Point(programsTitle.Right+Ui.Px(10),(programsHead.Height-selectionInfo.Height)/2+1);
   all.Location=new Point(programsHead.Width-all.Width-Ui.Px(4),(programsHead.Height-all.Height)/2);
   addProgram.Location=new Point(all.Left-Ui.Px(4)-addProgram.Width,(programsHead.Height-addProgram.Height)/2);
  };
  programsHead.Resize+=layoutHead;all.SizeChanged+=layoutHead;addProgram.SizeChanged+=layoutHead;
  list.Dock=DockStyle.Fill;
  programsCard.Controls.Add(list);programsCard.Controls.Add(programsHead);list.BringToFront();

  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,Ui.Px(14)),Padding=new Padding(0),BackColor=Ui.Bg,WrapContents=false};
  var alignLabel=new Label{Text="Ausrichten",AutoSize=true,Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  alignLabel.Margin=new Padding(0,Math.Max(0,(Ui.Px(38)-alignLabel.PreferredHeight)/2),Ui.Px(10),0);
  actions.Controls.AddRange(new Control[]{restore,save});

  // Zweite Zeile: Ausrichten an der sichtbaren Fensterkante mit frei wählbarem Abstand.
  var alignRow=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,Ui.Px(14)),Padding=new Padding(0),BackColor=Ui.Bg,WrapContents=false};
  var gapLabel=new Label{Text="Abstand",AutoSize=true,Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  gapLabel.Margin=new Padding(Ui.Px(8),Math.Max(0,(Ui.Px(38)-gapLabel.PreferredHeight)/2),Ui.Px(8),0);
  var pxLabel=new Label{Text="px",AutoSize=true,Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  pxLabel.Margin=new Padding(Ui.Px(6),Math.Max(0,(Ui.Px(38)-pxLabel.PreferredHeight)/2),0,0);
  var gapCard=new Card{Size=new Size(Ui.Px(56),Ui.Px(38)),Margin=new Padding(0),Padding=new Padding(Ui.Px(6),Ui.Px(9),Ui.Px(6),0)};
  gapBox.Dock=DockStyle.Fill;gapBox.BorderStyle=BorderStyle.None;gapBox.TextAlign=HorizontalAlignment.Center;gapBox.MaxLength=3;
  gapBox.Font=Ui.UiFont;gapBox.BackColor=Ui.Surface;gapBox.ForeColor=Ui.TextColor;
  gapBox.Text=SavedGap().ToString();
  gapBox.KeyPress+=(s,e)=>{if(!Char.IsDigit(e.KeyChar)&&!Char.IsControl(e.KeyChar))e.Handled=true;};
  gapBox.Leave+=(s,e)=>SaveGap();
  gapCard.Controls.Add(gapBox);
  alignRules=new ModernButton("Kanten je Programm",ButtonKind.Ghost,"\uE713");
  alignRules.Margin=new Padding(Ui.Px(8),0,0,0);
  alignRules.Click+=(s,e)=>EditEdges();
  alignRow.Controls.AddRange(new Control[]{alignLabel,alignTop,alignBottom,alignLeft,alignRight,new ActionSeparator(),alignAuto,gapLabel,gapCard,pxLabel,alignRules});
  alignTips.SetToolTip(alignRules,"Je Programm festlegen, welche Kanten ausgerichtet werden");
  alignTips.SetToolTip(alignTop,"Ausgewählte Fenster an die Oberkante des Arbeitsbereichs setzen");
  alignTips.SetToolTip(alignBottom,"Ausgewählte Fenster an die Unterkante des Arbeitsbereichs setzen (z.B. über die Taskleiste)");
  alignTips.SetToolTip(alignLeft,"Ausgewählte Fenster an den linken Rand setzen");
  alignTips.SetToolTip(alignRight,"Ausgewählte Fenster an den rechten Rand setzen");
  alignTips.SetToolTip(alignAuto,"Alle angehakten Kanten aus „Kanten je Programm“ auf einmal ausrichten\r\n(Oben + Unten = volle Höhe, Links + Rechts = volle Breite, Mitte = waagerecht mittig)");
  alignTips.SetToolTip(gapBox,"Abstand der sichtbaren Fensterkante zum Rand in Bildschirmpixeln (Standard 18)");

  hint.Dock=DockStyle.Fill;hint.Margin=new Padding(0,0,0,Ui.Px(14));
  // Drei kurze Zeilen, je ein Thema, damit nichts mitten im Satz umbricht.
  hint.Text="Ausgewählte Programme werden beendet und beim Wiederherstellen danach gestartet.\r\n"+
   "Easy2Sync und eigene Programme bleiben offen – nur ihr Fenster wird verschoben. Rechtsklick auf ein Programm: ausblenden bzw. entfernen.\r\n"+
   "TeamSpeak 3: vollständige Datenbanksicherung mit manueller Dateiauswahl.";

  var logCard=new Card{Dock=DockStyle.Fill,Margin=new Padding(0),Padding=new Padding(Ui.Px(16),Ui.Px(6),Ui.Px(8),Ui.Px(12))};
  var logTitle=new Label{Text="Protokoll",Dock=DockStyle.Top,Height=Ui.Px(36),Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,TextAlign=ContentAlignment.MiddleLeft};
  log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BorderStyle=BorderStyle.None;
  log.BackColor=Ui.Surface;log.ForeColor=Ui.LogText;log.Font=Ui.MonoFont;
  log.HandleCreated+=(s,e)=>Native.DarkScroll(log);
  logCard.Controls.Add(log);logCard.Controls.Add(logTitle);log.BringToFront();

  int programsHeight=ProgramsHeight(entries.Count);
  int fixedHeight=Ui.Px(76)+Ui.Px(17)+Ui.Px(52)+Ui.Px(52)+Ui.Px(88)+Ui.Px(64)+Ui.Px(18)+Ui.Px(2);
  layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(Ui.Px(24),Ui.Px(18),Ui.Px(24),Ui.Px(2)),ColumnCount=1,RowCount=8,BackColor=Ui.Bg};
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(76)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(17)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,programsHeight));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(52)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(52)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(88)));
  layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(64)));
  layout.Controls.Add(header,0,0);layout.Controls.Add(activity,0,1);layout.Controls.Add(programsCard,0,2);
  layout.Controls.Add(actions,0,3);layout.Controls.Add(alignRow,0,4);layout.Controls.Add(hint,0,5);layout.Controls.Add(logCard,0,6);layout.Controls.Add(footer,0,7);
  Controls.Add(layout);

  foreach(var e in entries) list.Add(new ProgramRow{Entry=e,Name=e.Name,Checked=false}); // Beim Start ist nichts angehakt.
  list.CheckedChanged+=(s,e)=>UpdateSelection();
  list.RowMenu+=ShowRowMenu;
  rowMenu.Opened+=(s,e)=>MenuStyle.RoundCorners(rowMenu);
  UpdateSelection();

  Rectangle work=Screen.PrimaryScreen.WorkingArea;
  ClientSize=new Size(Ui.Px(1080),fixedHeight+programsHeight+Ui.Px(230));
  int chrome=Height-ClientSize.Height;
  MinimumSize=new Size(Math.Min(Ui.Px(1000),work.Width),Math.Min(fixedHeight+programsHeight+Ui.Px(120)+chrome,work.Height));
  if(Height>work.Height)Height=work.Height;
  if(Width>work.Width)Width=work.Width;

  all.Click+=(s,e)=>{bool select=list.Rows.Any(x=>!x.Checked);foreach(var row in list.Rows)row.Checked=select;UpdateSelection();};
  restore.Click+=async(s,e)=>await Run(false);
  save.Click+=async(s,e)=>await Run(true);
  alignTop.Click+=async(s,e)=>await AlignWindows("top");
  alignBottom.Click+=async(s,e)=>await AlignWindows("bottom");
  alignLeft.Click+=async(s,e)=>await AlignWindows("left");
  alignRight.Click+=async(s,e)=>await AlignWindows("right");
  alignAuto.Click+=async(s,e)=>await AlignWindows("auto");
  folder.Click+=OpenBackups;
  addProgram.Click+=(s,e)=>AddProgram();
  FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
  ApplyTheme();
  Log(AppInfo.Product+" Version "+AppInfo.Version+" – Farbschema: "+(Ui.Dark?"dunkel":"hell")+", Akzentfarbe "+Ui.Hex(Ui.AccentBase));
  TraceLog.Cleanup(30);
  if(migrated!=null)Log(migrated);
  Log("Positionen: "+Store.Display(Store.Profiles));
  Log("Protokolldatei: "+Store.Display(TraceLog.Today));
  if(skipped!=null)Log("Nicht installiert (nicht angezeigt): "+skipped);
  if(hiddenNames!=null)Log("Ausgeblendet: "+hiddenNames+" – Rechtsklick in die Liste zeigt sie wieder an.");
  int customCount=entries.Count(x=>x.Custom);
  if(customCount>0)Log("Eigene Programme: "+customCount);
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Native.DarkTitle(Handle);}
 // Windows meldet einen Wechsel zwischen hellem und dunklem Modus mit WM_SETTINGCHANGE "ImmersiveColorSet".
 protected override void WndProc(ref Message m) {
  base.WndProc(ref m);
  // WM_DWMCOLORIZATIONCOLORCHANGED (0x320) meldet eine geänderte Akzentfarbe.
  if((m.Msg==0x001A&&m.LParam!=IntPtr.Zero&&Marshal.PtrToStringUni(m.LParam)=="ImmersiveColorSet")||m.Msg==0x0320) {
   bool oldDark=Ui.Dark;Color oldBase=Ui.AccentBase;
   Ui.ApplyPalette(Ui.DetectDark());
   if(Ui.Dark!=oldDark||Ui.AccentBase!=oldBase) {
    ApplyTheme();
    Log("Farbschema: "+(Ui.Dark?"dunkel":"hell")+", Akzentfarbe "+Ui.Hex(Ui.AccentBase));
   }
  }
 }
 void ApplyTheme() {
  BackColor=Ui.Bg;ForeColor=Ui.TextColor;
  ApplyTheme(this,false);
  log.ForeColor=Ui.LogText;
  if(IsHandleCreated) {Native.DarkTitle(Handle);Native.DarkScroll(log);Native.DarkScroll(list);}
  Invalidate(true);
 }
 static void ApplyTheme(Control parent,bool inCard) {
  foreach(Control control in parent.Controls) {
   bool card=inCard;
   if(control is Card){control.BackColor=Ui.Bg;card=true;}
   else if(!(control is ModernButton))control.BackColor=inCard?Ui.Surface:Ui.Bg;
   if(control is Label)control.ForeColor=(control.Tag as string)=="muted"?Ui.Muted:Ui.TextColor;
   if(control is TextBox)control.ForeColor=Ui.TextColor;
   ApplyTheme(control,card);
  }
 }
 // Das Logo kommt aus der eingebetteten ICO-Datei (Erstellen.bat) oder aus Resources\Fensterpositionen.ico.
 // Die ICO enthält PNG-Bilder; Icon.ToBitmap liest diese nicht zuverlässig, daher eigener Leser.
 void OpenBackups(object sender,EventArgs e){string p=Path.Combine(Store.Root,"Sicherungen");Directory.CreateDirectory(p);Process.Start("explorer.exe",Quote(p));}
 static bool IsElevated() {
  try {
   using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())
    return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
  } catch {return false;}
 }
 void RestartElevated() {
  if(busy)return;
  try {
   Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});
   Close();
  } catch(System.ComponentModel.Win32Exception) {
   Log("Start als Administrator abgebrochen.");
  }
 }
 void ShowInfo() {
  if(busy)return;
  using(var about=new AboutForm(signature,Icon,OpenBackups)) about.ShowDialog(this);
 }
 // Eingebettete Ressource suchen; der Name kann einen Ordner enthalten (z.B. "Resources\Fensterpositionen.ico").
 static Stream ResourceStream(string name) {
  var assembly=Assembly.GetExecutingAssembly();
  foreach(string resource in assembly.GetManifestResourceNames())
   if(resource.Equals(name,StringComparison.OrdinalIgnoreCase)||resource.EndsWith("\\"+name,StringComparison.OrdinalIgnoreCase)||resource.EndsWith("/"+name,StringComparison.OrdinalIgnoreCase))
    return assembly.GetManifestResourceStream(resource);
  return null;
 }
 // Datei im Ordner Resources neben der EXE, sonst direkt neben der EXE.
 static string ProjectFile(string name) {
  string inResources=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources",name);
  return System.IO.File.Exists(inResources)?inResources:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);
 }
 // Signatur-Logo: eingebettet (Erstellen.bat) oder Datei GS-Logo.png neben der EXE.
 internal static Bitmap LoadSignature() {
  try {
   using(var stream=ResourceStream("GS-Logo.png")) {
    if(stream!=null)using(var image=Image.FromStream(stream))return new Bitmap(image);
   }
   string file=ProjectFile("GS-Logo.png");
   if(System.IO.File.Exists(file))using(var image=Image.FromFile(file))return new Bitmap(image);
  } catch {}
  return null;
 }
 Bitmap LoadLogo(int size) {
  try {
   byte[] data=null;
   using(var stream=ResourceStream("Fensterpositionen.ico")) {
    if(stream!=null){var memory=new MemoryStream();stream.CopyTo(memory);data=memory.ToArray();}
   }
   if(data==null) {
    string ico=ProjectFile("Fensterpositionen.ico");
    if(System.IO.File.Exists(ico))data=System.IO.File.ReadAllBytes(ico);
   }
   if(data!=null) {
    Bitmap frame=IconFrame(data,size);
    if(frame!=null)return frame;
   }
  } catch {}
  try {return Icon.ToBitmap();} catch {return null;}
 }
 static Bitmap IconFrame(byte[] data,int size) {
  if(data.Length<6||BitConverter.ToUInt16(data,2)!=1)return null;
  int count=BitConverter.ToUInt16(data,4),best=-1,bestSize=0;
  for(int i=0;i<count;i++) {
   int entry=6+16*i;
   if(entry+16>data.Length)break;
   int width=data[entry]==0?256:data[entry];
   // Kleinste Größe ab der Zielgröße wählen, sonst die größte vorhandene.
   bool better=best<0||(bestSize<size?width>bestSize:(width>=size&&width<bestSize));
   if(better){best=i;bestSize=width;}
  }
  if(best<0)return null;
  int e=6+16*best;
  int length=BitConverter.ToInt32(data,e+8),offset=BitConverter.ToInt32(data,e+12);
  if(offset<0||length<=8||offset+length>data.Length)return null;
  if(data[offset]==0x89&&data[offset+1]==0x50&&data[offset+2]==0x4E&&data[offset+3]==0x47) {
   using(var stream=new MemoryStream(data,offset,length))
   using(var image=Image.FromStream(stream)) return new Bitmap(image);
  }
  using(var stream=new MemoryStream(data))
  using(var icon=new Icon(stream,bestSize,bestSize)) return icon.ToBitmap();
 }
 // Demo-Ablauf für die Screenshots: eigenes Programm hinzugefügt, danach "Unten ausrichten" für einige Programme.
 internal void DemoRun() {
  var demo=new Entry{Id="eigen-notepad",Name="Editor",ProcessName="notepad",Exe=@"C:\Windows\System32\notepad.exe",WindowOnly=true,Custom=true,Keys=new[]{"Window"}};
  entries.Add(demo);
  list.Add(new ProgramRow{Entry=demo,Name=demo.Name,Checked=false});
  UpdateProgramsHeight();
  var states=new Dictionary<string,string>{
   {"taskmanager","Unten: 18 px"},{"dumeter","Ausrichten übersprungen"},
   {"franz","Unten: 18 px"},{"easy2sync","Ausrichten übersprungen"}};
  foreach(var row in list.Rows) {
   string status;
   if(states.TryGetValue(row.Entry.Id,out status)){row.Checked=true;row.Status=status;}
   else if(row.Entry.Id==demo.Id)row.Status="Position gespeichert";
  }
  UpdateSelection();
  Log("Programm hinzugefügt: Editor (notepad.exe)");
  Log("Editor: Position gespeichert");
  Log("Unten ausrichten (18 px) für: Task-Manager, DU Meter, Franz, Easy2Sync");
  Log("Task-Manager: Unten: 18 px");
  Log("Franz: Unten: 18 px");
  Log("Zum dauerhaften Übernehmen der ausgerichteten Fenster jetzt Positionen speichern.");
 }
 void UpdateSelection() {
  int count=list.Rows.Count(x=>x.Checked);
  selectionInfo.Text=count+" von "+list.Rows.Count+" ausgewählt";
  all.Text=list.Rows.Any(x=>!x.Checked)?"Alle auswählen":"Auswahl aufheben";
 }
 void SetBusy(bool on) {
  busy=on;list.Enabled=!on;
  foreach(var button in new Button[]{restore,save,all,alignTop,alignBottom,alignLeft,alignRight,alignAuto,alignRules,folder,admin,addProgram})if(button!=null)button.Enabled=!on;
  activity.Active=on;
 }
 static string Quote(string s){return "\""+s+"\"";}
 void Setup() {
  string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  string roam=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  entries.Add(new Entry{Id="taskmanager",Name="Task-Manager",ProcessName="Taskmgr",Exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","Taskmgr.exe"),File=Path.Combine(local,"Microsoft","Windows","TaskManager","settings.json"),Keys=new[]{"FullPosition","SmallPosition","FullViewDefaultSize"}});
  entries.Add(new Entry{Id="dumeter",Name="DU Meter",ProcessName="DUMeter",Exe=@"C:\Program Files (x86)\DU Meter\DUMeter.exe",Registry=true,RegistryPath=@"Software\Hagel\DU Meter\Window",Keys=new[]{"WindowPosition"}});
  entries.Add(new Entry{Id="franz",Name="Franz",ProcessName="Franz",Exe=Path.Combine(local,"Programs","franz","Franz.exe"),File=Path.Combine(roam,"Franz","app-state.json"),Keys=new[]{"windowState"}});
  entries.Add(new Entry{Id="ts3",Name="TeamSpeak 3",ProcessName="ts3client_win64",Exe=@"C:\Program Files (x86)\TeamSpeak 3\ts3client_win64.exe",File=@"C:\Program Files (x86)\TeamSpeak 3\config\settings.db"});
  entries.Add(new Entry{Id="ts6",Name="TeamSpeak 6",ProcessName="TeamSpeak",Exe=@"C:\Program Files\TeamSpeak 6\TeamSpeak.exe",File=Path.Combine(roam,"TeamSpeak","Default","placement.json"),Keys=new[]{"ts_main_window"}});
  entries.Add(new Entry{Id="discord",Name="Discord",ProcessName="Discord",Exe=Path.Combine(local,"Discord","Update.exe"),Args="--processStart Discord.exe",File=Path.Combine(roam,"discord","settings.json"),Keys=new[]{"WINDOW_BOUNDS","IS_MAXIMIZED","IS_MINIMIZED"}});
  string e2sDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Easy2Sync");
  string e2sExe=Path.Combine(e2sDir,Environment.Is64BitOperatingSystem&&System.IO.File.Exists(Path.Combine(e2sDir,"Easy2Sync64Bit.exe"))?"Easy2Sync64Bit.exe":"Easy2Sync.exe");
  entries.Add(new Entry{Id="easy2sync",Name="Easy2Sync",ProcessName=Path.GetFileNameWithoutExtension(e2sExe),AltProcessName=Path.GetFileNameWithoutExtension(e2sExe)=="Easy2Sync"?"Easy2Sync64Bit":"Easy2Sync",Exe=e2sExe,WindowOnly=true,Keys=new[]{"Window"}});
  entries.Add(new Entry{Id="gpuz",Name="GPU-Z",ProcessName="GPU-Z",Exe=@"C:\Program Files (x86)\GPU-Z\GPU-Z.exe",Registry=true,RegistryPath=@"Software\techPowerUp\GPU-Z",Keys=new[]{"WindowPos","WindowSizeResizable"}});
 }
 static void RestartDUMeterService() {
  using(var service=new System.ServiceProcess.ServiceController("DUMeterSvc")) {
   service.Refresh();
   if(service.Status==System.ServiceProcess.ServiceControllerStatus.StopPending)
    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));
   service.Refresh();
   if(service.Status!=System.ServiceProcess.ServiceControllerStatus.Stopped) {
    if(service.Status==System.ServiceProcess.ServiceControllerStatus.StartPending)
     service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));
    service.Stop();
    service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));
   }
   service.Start();
   service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));
  }
 }
 // Abstand: zuletzt verwendeter Wert aus Positionen.json ("_gap"), sonst 18.
 int SavedGap() {
  try {if(profiles!=null&&profiles.ContainsKey("_gap"))return ClampGap(Convert.ToInt32(profiles["_gap"]));} catch {}
  return DefaultGap;
 }
 static int ClampGap(int value){return Math.Max(0,Math.Min(200,value));}
 int CurrentGap() {
  int value;
  if(!Int32.TryParse(gapBox.Text.Trim(),out value))value=DefaultGap;
  value=ClampGap(value);
  if(gapBox.Text!=value.ToString())gapBox.Text=value.ToString();
  return value;
 }
 void SaveGap() {
  int gap=CurrentGap();
  if(gap==SavedGap()&&profiles.ContainsKey("_gap"))return;
  if(DemoMode)return;
  try {
   var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
   copy["_gap"]=gap;
   Store.Write(Store.Profiles,copy);
   profiles=copy;
  } catch(Exception ex) {Log("Abstand konnte nicht gespeichert werden: "+ex.Message);}
 }
 // Gespeicherte Regel "Kanten" oder "Kanten:Abstand" (ältere Regeln ohne Abstand: 18 px).
 static void ParseRule(string rule,out string edges,out int gap) {
  edges=rule??"";gap=DefaultGap;
  int colon=edges.IndexOf(':');
  if(colon>=0) {
   int value;
   if(Int32.TryParse(edges.Substring(colon+1),out value))gap=ClampGap(value);
   edges=edges.Substring(0,colon);
  }
  edges=edges.Trim();
 }
 // Einzelne Richtung in eine vorhandene Regel einfügen: gleiche Achse ersetzen, andere Achse behalten.
 static string MergeRule(string oldEdges,string direction) {
  bool vertical=direction=="top"||direction=="bottom";
  var keep=new List<string>();
  foreach(string edge in (oldEdges??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)) {
   bool edgeVertical=edge=="top"||edge=="bottom";
   if(edgeVertical!=vertical)keep.Add(edge);
  }
  keep.Add(direction);
  var order=new[]{"top","bottom","left","right","hcenter"};
  return String.Join(" ",order.Where(x=>keep.Contains(x)).ToArray());
 }
 // Kanten je Programm (Fenster "Kanten je Programm"): angehakte Kanten werden bei "Automatisch" zusammen
 // ausgerichtet; die Schaltflächen Oben/Unten/Links/Rechts richten nur Programme aus, bei denen die Kante angehakt ist.
 // Oben + Unten = volle Höhe, Links + Rechts = volle Breite, Mitte = waagerecht mittig.
 // Gespeichert in Positionen.json ("_edges"); ohne Einstellung gelten diese Vorgaben (Verhalten bis 2.23).
 internal static readonly string[] AllEdges={"top","bottom","left","right","hcenter"};
 internal static readonly string[] StandardEdges={"top","bottom","left","right"};
 internal static readonly Dictionary<string,string[]> DefaultEdges=new Dictionary<string,string[]>{
  {"taskmanager",new[]{"bottom","right"}},
  {"dumeter",new[]{"top","right"}},
  {"franz",new[]{"top","bottom","left"}},
  {"ts3",new[]{"top"}},
  {"ts6",new[]{"top","bottom"}},
  {"discord",new[]{"bottom","left"}},
  {"easy2sync",new[]{"hcenter"}},
  {"gpuz",new[]{"bottom"}}
 };
 // Vorgabe für ein Programm: Tabelle oben, sonst (z.B. eigene Programme) alle vier Kanten ohne Mitte.
 internal static HashSet<string> DefaultEdgesFor(string id) {
  string[] edges;
  return new HashSet<string>(DefaultEdges.TryGetValue(id,out edges)?edges:StandardEdges);
 }
 HashSet<string> EdgesFor(string id) {
  var saved=profiles!=null&&profiles.ContainsKey("_edges")?profiles["_edges"] as Dictionary<string,object>:null;
  if(saved!=null&&saved.ContainsKey(id)) {
   var set=new HashSet<string>();
   var items=saved[id] as System.Collections.IEnumerable;
   if(items!=null&&!(saved[id] is string))foreach(object item in items)if(item!=null)set.Add(Convert.ToString(item));
   return set;
  }
  return DefaultEdgesFor(id);
 }
 bool Skipped(string mode,string id) {
  return mode!="auto"&&!EdgesFor(id).Contains(mode);
 }
 // Kanten für "Automatisch" in fester Reihenfolge; Mitte nur, wenn weder links noch rechts angehakt ist.
 string AutoEdges(string id) {
  var set=EdgesFor(id);
  if(set.Contains("left")||set.Contains("right"))set.Remove("hcenter");
  return String.Join(" ",AllEdges.Where(x=>set.Contains(x)).ToArray());
 }
 void EditEdges() {
  if(busy)return;
  var current=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var e in entries)current[e.Id]=EdgesFor(e.Id);
  Dictionary<string,HashSet<string>> result;
  using(var dialog=new AlignRulesForm(Icon,entries,current)) {
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   result=dialog.Rules;
  }
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var saved=copy.ContainsKey("_edges")?copy["_edges"] as Dictionary<string,object>:null;
  if(saved==null){saved=new Dictionary<string,object>();copy["_edges"]=saved;}
  foreach(var pair in result)saved[pair.Key]=AllEdges.Where(x=>pair.Value.Contains(x)).ToList();
  copy.Remove("_skip");
  try {Store.Write(Store.Profiles,copy);}
  catch(Exception ex) {Log("Kanten je Programm konnten nicht gespeichert werden: "+ex.Message);return;}
  profiles=copy;
  var lines=new List<string>();
  foreach(var e in entries) {
   HashSet<string> set;
   if(result.TryGetValue(e.Id,out set))lines.Add(e.Name+": "+(set.Count>0?String.Join(", ",AllEdges.Where(x=>set.Contains(x)).Select(x=>DirectionName(x)).ToArray()):"keine"));
  }
  Log("Kanten je Programm gespeichert – "+String.Join("; ",lines.ToArray()));
 }
 static string DirectionName(string mode) {
  switch(mode){case "top":return "Oben";case "bottom":return "Unten";case "left":return "Links";case "right":return "Rechts";case "hcenter":return "Mitte";default:return "Automatisch";}
 }
 async Task AlignWindows(string mode) {
  var selected=list.Rows.Where(x=>x.Checked).ToList();
  if(selected.Count==0){MessageBox.Show(this,"Bitte mindestens ein Programm auswählen.");return;}
  int gap=CurrentGap();
  SaveGap();
  SetBusy(true);
  try {
   // Nur die angehakten Programme werden ausgerichtet; alle anderen bleiben unverändert.
   Log(DirectionName(mode)+" ausrichten ("+gap+" px) für: "+String.Join(", ",selected.Select(x=>x.Name).ToArray()));
   foreach(var item in selected) {
    var entry=item.Entry;
    string targetEdges=mode;
    if(Skipped(mode,entry.Id)){item.Status="Ausrichten übersprungen";continue;}
    if(mode=="auto") {
     // Alle angehakten Kanten aus "Kanten je Programm" auf einmal.
     targetEdges=AutoEdges(entry.Id);
     if(targetEdges.Length==0){item.Status="Ausrichten übersprungen";continue;}
    }
    item.Status="Fensterkante wird gemessen …";
    try {
     string edges=targetEdges;
     string status=await Task.Run(()=>VisibleAlignment.AlignEntry(entry,gap,edges));
     if(edges.Contains("hcenter")) {
      // Bei Easy2Sync auch weitere offene Fenster (z.B. Synchronisation) waagerecht mittig setzen.
      // Weitere Fenster nur waagerecht mittig (ihre Höhe und senkrechte Lage bleiben).
      var extra=await Task.Run(()=>VisibleAlignment.AlignOthers(entry,gap,"hcenter"));
      foreach(string line in extra)Log(entry.Name+" – "+line);
      if(extra.Count>0)status+="; "+extra.Count+" weitere(s) Fenster";
     }
     var alignedProfiles=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
     Dictionary<string,object> rules;
     if(alignedProfiles.ContainsKey("_alignment"))rules=(Dictionary<string,object>)alignedProfiles["_alignment"];
     else {rules=new Dictionary<string,object>();alignedProfiles["_alignment"]=rules;}
     // Regel für die Nachkorrektur beim Start: einzelne Richtungen ergänzen die bisherige Regel.
     string oldEdges;int oldGap;
     ParseRule(rules.ContainsKey(entry.Id)?Convert.ToString(rules[entry.Id]):"",out oldEdges,out oldGap);
     string rule=mode=="auto"?edges:MergeRule(oldEdges,mode);
     rules[entry.Id]=rule+":"+gap;
     Store.Write(Store.Profiles,alignedProfiles);
     profiles=alignedProfiles;
     if(entry.Id=="dumeter") {
      Log("DU Meter ist ausgerichtet. Dienst DUMeterSvc wird neu gestartet …");
      try {await Task.Run(()=>RestartDUMeterService());status+="; Dienst neu gestartet";}
      catch(Exception serviceError){throw new Exception("Fenster ausgerichtet; Dienst-Neustart fehlgeschlagen: "+serviceError.Message);}
     }
     item.Status=status;
     Log(entry.Name+": "+status);
    }catch(Exception ex){item.Status=ex.Message.StartsWith("Fenster ausgerichtet;")?"Ausgerichtet; Dienstfehler – siehe Protokoll":"Nicht ausgerichtet – siehe Protokoll";Log(entry.Name+": "+ex.Message);}
   }
   Log("Zum dauerhaften Übernehmen der ausgerichteten Fenster jetzt Positionen speichern.");
  } finally {SetBusy(false);}
 }
 // Höhe des Programmbereichs: bis zu 12 Zeilen sichtbar, darüber wird die Liste gescrollt.
 int ProgramsHeight(int count){return list.RowHeight*Math.Max(1,Math.Min(count,12))+Ui.Px(44)+Ui.Px(16)+Ui.Px(14)+2;}
 // Nach Hinzufügen/Entfernen: Programmbereich anpassen, das Fenster wächst bzw. schrumpft mit (begrenzt auf den Bildschirm).
 void UpdateProgramsHeight() {
  if(layout==null)return;
  int target=ProgramsHeight(list.Rows.Count);
  int delta=target-(int)layout.RowStyles[2].Height;
  if(delta==0)return;
  layout.RowStyles[2].Height=target;
  Rectangle work=Screen.FromControl(this).WorkingArea;
  int oldHeight=Height;
  MinimumSize=new Size(MinimumSize.Width,Math.Max(Ui.Px(300),Math.Min(MinimumSize.Height+delta,work.Height)));
  if(WindowState==FormWindowState.Normal) {
   Height=Math.Max(MinimumSize.Height,Math.Min(oldHeight+delta,work.Height));
   if(Bottom>work.Bottom)Top=Math.Max(work.Top,work.Bottom-Height);
  }
 }
 void AddProgram() {
  if(busy)return;
  Entry entry;
  using(var dialog=new AddProgramForm(Icon,entries)) {
   if(dialog.ShowDialog(this)!=DialogResult.OK||dialog.Result==null)return;
   entry=dialog.Result;
  }
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  string status="Hinzugefügt – Position noch nicht gespeichert";
  string note=null;
  try {
   // Aktuelle Fensterposition gleich mitspeichern.
   copy[entry.Id]=new Dictionary<string,object>{{"Window",VisibleAlignment.CaptureWindow(entry)}};
   status="Position gespeichert";
  } catch(Exception ex) {note=ex.Message;}
  var updated=new List<Entry>(entries);updated.Add(entry);
  Store.SetCustomEntries(copy,updated);
  try {Store.Write(Store.Profiles,copy);}
  catch(Exception ex) {Log(entry.Name+": konnte nicht gespeichert werden – "+ex.Message);return;}
  profiles=copy;
  entries.Add(entry);
  list.Add(new ProgramRow{Entry=entry,Name=entry.Name,Checked=false});
  UpdateProgramsHeight();
  UpdateSelection();
  list.Rows[list.Rows.Count-1].Status=status;
  Log("Programm hinzugefügt: "+entry.Name+" ("+entry.ProcessName+".exe"+(String.IsNullOrEmpty(entry.Exe)?", Programmpfad unbekannt – wird nicht automatisch gestartet":"")+")");
  if(note!=null)Log(entry.Name+": "+note+" Danach \"Positionen speichern\" verwenden.");
  else Log(entry.Name+": "+status);
 }
 // Programm gilt als installiert, wenn die Programmdatei existiert oder es gerade läuft.
 static bool Installed(Entry e) {
  if(e.Custom)return true;
  try {
   if(!String.IsNullOrEmpty(e.Exe)&&System.IO.File.Exists(e.Exe))return true;
   foreach(string name in new[]{e.ProcessName,e.AltProcessName}) {
    if(String.IsNullOrEmpty(name))continue;
    var running=Process.GetProcessesByName(name);
    bool any=running.Length>0;
    foreach(var p in running)p.Dispose();
    if(any)return true;
   }
  } catch {}
  return false;
 }
 // Selbst ausgeblendete, installierte Programme (für "wieder anzeigen").
 List<Entry> HiddenBuiltIns() {
  var hidden=Store.HiddenIds(profiles);
  return builtIns.Where(x=>hidden.Contains(x.Id)&&!entries.Contains(x)&&Installed(x)).ToList();
 }
 void ShowRowMenu(object sender,RowMenuEventArgs e) {
  if(busy)return;
  var row=e.Row;
  var hidden=HiddenBuiltIns();
  // Menü bei jedem Öffnen neu aufbauen, damit Farben und Symbol zum aktuellen Hell-/Dunkelmodus passen.
  MenuStyle.Prepare(rowMenu);
  rowMenu.Items.Add(MenuStyle.TopSpacer());
  if(row!=null&&row.Entry!=null) {
   if(row.Entry.Custom)rowMenu.Items.Add(MenuStyle.Item("„"+row.Name+"“ entfernen",'\uE74D',(s,a)=>RemoveProgram(row)));
   else rowMenu.Items.Add(MenuStyle.Item("„"+row.Name+"“ ausblenden",'\uED1A',(s,a)=>HideProgram(row)));
  }
  if(hidden.Count>0) {
   if(row!=null&&row.Entry!=null)rowMenu.Items.Add(new ToolStripSeparator());
   rowMenu.Items.Add(MenuStyle.Item("Ausgeblendete wieder anzeigen ("+hidden.Count+")",'\uE890',(s,a)=>ShowHiddenPrograms()));
  }
  if(rowMenu.Items.Count>1)rowMenu.Items.Add(new ToolStripSeparator());
  rowMenu.Items.Add(MenuStyle.Item("Kanten je Programm …",'\uE713',(s,a)=>EditEdges()));
  rowMenu.Items.Add(MenuStyle.BottomSpacer());
  rowMenu.Show(list,e.Location);
 }
 // Fest eingebautes Programm ausblenden; gespeicherte Werte bleiben erhalten.
 void HideProgram(ProgramRow row) {
  if(busy)return;
  var entry=row.Entry;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var ids=Store.HiddenIds(copy);ids.Add(entry.Id);
  Store.SetHiddenIds(copy,ids);
  try {Store.Write(Store.Profiles,copy);}
  catch(Exception ex) {Log(entry.Name+": konnte nicht ausgeblendet werden – "+ex.Message);return;}
  profiles=copy;
  entries.Remove(entry);
  list.Remove(row);
  UpdateProgramsHeight();
  UpdateSelection();
  Log("Ausgeblendet: "+entry.Name+" (Rechtsklick in die Liste zeigt es wieder an; gespeicherte Positionen bleiben erhalten)");
 }
 void ShowHiddenPrograms() {
  if(busy)return;
  var back=HiddenBuiltIns();
  if(back.Count==0)return;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var ids=Store.HiddenIds(copy);
  foreach(var e in back)ids.Remove(e.Id);
  Store.SetHiddenIds(copy,ids);
  try {Store.Write(Store.Profiles,copy);}
  catch(Exception ex) {Log("Ausgeblendete Programme konnten nicht angezeigt werden – "+ex.Message);return;}
  profiles=copy;
  foreach(var e in back) {
   // An der ursprünglichen Stelle einfügen (Reihenfolge der fest eingebauten Programme, eigene danach).
   int pos=entries.Count(x=>!x.Custom&&builtIns.IndexOf(x)<builtIns.IndexOf(e));
   entries.Insert(pos,e);
   list.Insert(pos,new ProgramRow{Entry=e,Name=e.Name,Checked=false});
  }
  UpdateProgramsHeight();
  UpdateSelection();
  Log("Wieder angezeigt: "+String.Join(", ",back.Select(x=>x.Name).ToArray()));
 }
 void RemoveProgram(ProgramRow row) {
  if(busy)return;
  var entry=row.Entry;
  if(MessageBox.Show(this,"„"+entry.Name+"“ aus der Liste entfernen?\r\nDie gespeicherte Fensterposition wird ebenfalls gelöscht; das Programm selbst bleibt unverändert.","Programm entfernen",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  copy.Remove(entry.Id);
  if(copy.ContainsKey("_alignment")) {
   var rules=copy["_alignment"] as Dictionary<string,object>;
   if(rules!=null)rules.Remove(entry.Id);
  }
  Store.SetCustomEntries(copy,entries.Where(x=>x!=entry));
  try {Store.Write(Store.Profiles,copy);}
  catch(Exception ex) {Log(entry.Name+": konnte nicht entfernt werden – "+ex.Message);return;}
  profiles=copy;
  entries.Remove(entry);
  list.Remove(row);
  UpdateProgramsHeight();
  UpdateSelection();
  Log("Programm entfernt: "+entry.Name);
 }
 // Protokoll im Fenster und zusätzlich als Tagesdatei in TraceLogs.
 void Log(string text){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);TraceLog.Write(text);}
 bool Stop(Entry e) {
  if(e.Id=="taskmanager") {
   using(var task=Process.Start(new ProcessStartInfo("schtasks.exe","/End /TN \"Task Manager starten\""){CreateNoWindow=true,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})) { task.WaitForExit(3000); }
  }
  bool running=false;
  var initial=Process.GetProcessesByName(e.ProcessName);
  foreach(var p in initial) using(p) {
   try { if(!p.HasExited){running=true;p.CloseMainWindow();} }
   catch(InvalidOperationException){}
  }
  if(!running)return false;
  // Electron applications may hide in the tray and spawn helper processes.
  // Allow a normal exit first, then repeatedly collect and stop remaining processes.
  var timer=Stopwatch.StartNew();
  while(timer.ElapsedMilliseconds<20000) {
   var processes=Process.GetProcessesByName(e.ProcessName);
   if(processes.Length==0) {
    Thread.Sleep(500);
    var check=Process.GetProcessesByName(e.ProcessName);
    bool empty=check.Length==0;
    foreach(var p in check)p.Dispose();
    if(empty)return true;
    continue;
   }
   foreach(var p in processes) using(p) {
    try {
     if(p.HasExited)continue;
     if(timer.ElapsedMilliseconds<2500) p.CloseMainWindow();
     else {p.Kill();p.WaitForExit(1500);}
    } catch(InvalidOperationException){} // Process exited between enumeration and access.
   }
   Thread.Sleep(250);
  }
  var remaining=Process.GetProcessesByName(e.ProcessName);
  string ids=String.Join(", ",remaining.Select(p=>p.Id.ToString()).ToArray());
  foreach(var p in remaining)p.Dispose();
  if(remaining.Length==0)return true;
  throw new Exception("Programm ist nach 20 Sekunden noch aktiv (PID: "+ids+"). Bitte vollstaendig beenden und erneut versuchen.");
 }
 string StartupRule(Entry e) {
  if(profiles.ContainsKey("_alignment")) {
   var rules=profiles["_alignment"] as Dictionary<string,object>;
   if(rules!=null && rules.ContainsKey(e.Id))return Convert.ToString(rules[e.Id]);
  }
  // Existing profiles predate saved alignment rules.
  if(e.Id=="franz")return "top bottom left";
  if(e.Id=="discord")return "bottom left";
  return null;
 }
 void Start(Entry e,bool afterRestore=false) {
  try {StartWithAlignment(e,afterRestore);}
  finally {
   if(afterRestore && e.Id=="dumeter") {
    try {
     RestartDUMeterService();
     BeginInvoke((Action)(()=>Log("DU Meter: DUMeterSvc nach Wiederherstellen neu gestartet.")));
    } catch(Exception ex) {
     throw new Exception("DU-Meter-Wiederherstellung bearbeitet; Neustart von DUMeterSvc fehlgeschlagen: "+ex.Message,ex);
    }
   }
  }
 }
 void StartWithAlignment(Entry e,bool afterRestore) {
  if(!System.IO.File.Exists(e.Exe))throw new FileNotFoundException("Programmdatei fehlt: "+e.Exe);
  Process.Start(new ProcessStartInfo(e.Exe,e.Args??""){WorkingDirectory=Path.GetDirectoryName(e.Exe),UseShellExecute=true});
  string rule;int gap;
  ParseRule(StartupRule(e),out rule,out gap);
  if(String.IsNullOrEmpty(rule))return;
  BeginInvoke((Action)(()=>Log(e.Name+": gestartet; Fensterkante wird waehrend des Starts nachkorrigiert …")));
  // Reapply through the loading phase: Electron can change bounds after its first window appears.
  var timer=Stopwatch.StartNew();
  string lastError="Kein fertiges Fenster gefunden.";
  bool success=false;
  Thread.Sleep(2000);
  while(timer.ElapsedMilliseconds<25000) {
   try {
    VisibleAlignment.AlignEntry(e,gap,rule);
    success=true;
    lastError=null;
    // Keep watching through at least twelve seconds of startup.
    if(timer.ElapsedMilliseconds>=12000) {
     Thread.Sleep(1500);
     string result=VisibleAlignment.AlignEntry(e,gap,rule);
     if(e.Id=="dumeter" && !afterRestore)RestartDUMeterService();
     BeginInvoke((Action)(()=>Log(e.Name+": nach Start geprueft – "+result)));
     return;
    }
   } catch(Exception ex){lastError=ex.Message;success=false;}
   Thread.Sleep(1000);
  }
  throw new Exception("Programm gestartet; Ausrichtung nach Start nicht bestaetigt: "+(lastError??(success?"Startphase nicht abgeschlossen.":"Fenster nicht bereit.")));
 }
 async Task Run(bool capture) {
  var selected=list.Rows.Where(x=>x.Checked).ToList();
  if(selected.Count==0){MessageBox.Show(this,"Bitte mindestens ein Programm auswählen.");return;}
  string tsBackup=null;
  if(!capture && selected.Any(x=>x.Entry.Id=="ts3")) {
   using(var dialog=new OpenFileDialog{Title="TeamSpeak-3-Datenbank auswählen (ersetzt alle gespeicherten Einstellungen)",Filter="TeamSpeak-Datenbank (*.db)|*.db",InitialDirectory=@"C:\Program Files (x86)\TeamSpeak 3\config\backup"}) {
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    tsBackup=dialog.FileName;
   }
   // SQLite file signature check before stopping any application.
   using(var stream=System.IO.File.OpenRead(tsBackup)){
    byte[] header=new byte[16]; if(stream.Read(header,0,16)!=16 || Encoding.ASCII.GetString(header)!="SQLite format 3\0"){MessageBox.Show(this,"Die Datei ist keine gültige SQLite-Datenbank.");return;}
   }
  }
  SetBusy(true);
  int failures=0;
  try {
   foreach(var item in selected) {
    var entry=item.Entry; item.Status="Wird bearbeitet …";
    try {
     var profileCopy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
     string chosen=tsBackup;
     await Task.Run(()=>{
      // Fensterprogramme (Easy2Sync, eigene) brauchen die Programmdatei nur, wenn sie gestartet werden müssen.
      if(!entry.WindowOnly && !System.IO.File.Exists(entry.Exe))throw new FileNotFoundException("Programmdatei fehlt: "+entry.Exe);
      if(!capture && entry.Id!="ts3" && !profileCopy.ContainsKey(entry.Id))throw new Exception("Keine Position gespeichert.");
      if(entry.WindowOnly) {
       // Fenster direkt messen bzw. verschieben; das Programm wird dafür nicht beendet.
       if(capture) {
        profileCopy[entry.Id]=new Dictionary<string,object>{{"Window",VisibleAlignment.CaptureWindow(entry)}};
        Store.Write(Store.Profiles,profileCopy);profiles=profileCopy;
       } else {
        var saved=(Dictionary<string,object>)profileCopy[entry.Id];
        if(!saved.ContainsKey("Window"))throw new Exception("Keine Fensterposition gespeichert.");
        string placed=VisibleAlignment.RestoreWindow(entry,(Dictionary<string,object>)saved["Window"]);
        BeginInvoke((Action)(()=>Log(entry.Name+": "+placed)));
       }
       return;
      }
      bool running=Stop(entry);
      try {
       if(entry.Id=="ts3") {
        string backup=Store.BackupFile(entry.File,entry.Id);
        if(capture) profileCopy[entry.Id]=new Dictionary<string,object>{{"backup",backup}};
        else {
         if(String.Equals(Path.GetFullPath(chosen),Path.GetFullPath(entry.File),StringComparison.OrdinalIgnoreCase))throw new Exception("Bitte eine separate Backupdatei auswählen.");
         foreach(string suffix in new[]{"-wal","-shm","-journal"}) if(System.IO.File.Exists(entry.File+suffix))throw new Exception("SQLite-Begleitdatei vorhanden. Datenbank vorsichtshalber nicht ersetzt: "+suffix);
         System.IO.File.Copy(chosen,entry.File,true);
        }
       } else if(capture) profileCopy[entry.Id]=Store.Capture(entry);
       else Store.Restore(entry,(Dictionary<string,object>)profileCopy[entry.Id]);
       if(capture) {Store.Write(Store.Profiles,profileCopy);profiles=profileCopy;}
      } finally {if(capture && running)Start(entry);}
      if(!capture)Start(entry,true);
     });
     if(capture)profiles=profileCopy;
     item.Status=capture?"Position gespeichert":entry.WindowOnly?"Fenster verschoben":"Wiederhergestellt und gestartet";
     Log(entry.Name+": "+item.Status);
    } catch(Exception ex){failures++;item.Status="Fehler – siehe Protokoll";Log(entry.Name+": "+ex.Message);}
   }
   Log("Fertig. "+(selected.Count-failures)+" erfolgreich, "+failures+" Fehler.");
  } finally {SetBusy(false);}
 }
}
}
