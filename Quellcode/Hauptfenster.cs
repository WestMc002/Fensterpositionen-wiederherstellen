// Fensterpositionen wiederherstellen – Hauptfenster mit Navigation, Programmliste, Aktionen und Protokoll
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
 ModernButton restore,save,all,admin,addProgram;
 // Ausrichten: je eine Schaltfläche pro Kante und "Automatisch" (Regeln je Programm), Abstand frei wählbar.
 ModernButton alignTop,alignBottom,alignLeft,alignRight,alignHCenter,alignVCenter,alignAuto,alignRules;
 readonly TextBox gapBox=new TextBox();
 readonly ToolTip alignTips=new ModernToolTip();
 const int DefaultGap=18;
 TableLayoutPanel layout;
 readonly ContextMenuStrip rowMenu=new ContextMenuStrip();
 TextBox log=new TextBox();
 InfoBar hint=new InfoBar();
 // Update-Hinweis oben (nur sichtbar, wenn auf GitHub eine neuere Version liegt).
 readonly UpdateBar updateBar=new UpdateBar();
 ReleaseInfo available;
 // Ergebnis der letzten Prüfung, wenn es kein normaler Stand ist (Fehler oder "Nur Vorabversionen"), sonst null.
 UpdateStatus updateError;
 bool updateRunning,installing;
 // Prüfung still ca. 15 s nach jedem Start (ohne 20-Stunden-Sperre); "Automatisch aktualisieren" versucht es
 // alle 5 Minuten erneut, solange das Programm beschäftigt ist.
 readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer{Interval=15000};
 readonly System.Windows.Forms.Timer autoUpdateRetry=new System.Windows.Forms.Timer{Interval=5*60*1000};
 // Gesetzt beim Neustart nach einem Update (--nach-update).
 public static bool AfterUpdate;
 // Versuchskennung aus „--nach-update <Kennung>“ (ab 2.68), sonst null: Ergebnis aus der Datei dieses Versuchs.
 public static string UpdateAttemptId;
 ActivityBar activity=new ActivityBar();
 FooterPanel footer=new FooterPanel();
 Bitmap signature;
 Label selectionInfo=new Label();
 List<Entry> entries=new List<Entry>();
 // Vollständige Programmliste aus Positionen.json ("_programme") in ihrer Reihenfolge (auch ausgeblendete
 // und nicht installierte); entries enthält davon nur die angezeigten.
 readonly List<Entry> allPrograms=new List<Entry>();
 Dictionary<string,object> profiles;
 bool busy;
 // Navigation links (Globale Richtlinien; Vorlage: Wake on LAN): angepinnt = immer offen und schiebt den Inhalt;
 // sonst nur Symbole, ☰ klappt sie über dem Inhalt aus, nach der Auswahl bzw. beim Verlassen klappt sie wieder ein.
 Panel nav,navSpacer,headerHost,footerHost;
 NavButton navMenu,navPin,navPrograms,navEdges,navOneTime,navMore;
 readonly System.Windows.Forms.Timer navTimer=new System.Windows.Forms.Timer{Interval=400};
 bool navOpen;
 // Seiten im selben Fenster: "Kanten je Programm", "Einmalig ausrichten" und "Programm hinzufügen" sind die
 // bisherigen Dialoge, ohne Rahmen im Inhaltsbereich eingebettet (wie Wake on LAN, ShowView).
 Panel viewHost,viewBody;
 Label viewTitle;
 Form view;
 Action<DialogResult> viewDone;
 // Anordnungen (Store.Anordnungen): Auswahlfeld in der Aktionszeile, aktive Anordnung.
 ModernButton arrangement;
 readonly ContextMenuStrip arrangementMenu=new ContextMenuStrip();
 string activeArrangement=Store.StandardName;
 // Aufruf mit --wiederherstellen [Anordnung]: minimiert starten, wiederherstellen und bei Erfolg beenden.
 public static bool AutoRestore;
 // Breite des Inhalts ohne Navigation (die Ausrichten-Zeile passt gerade hinein).
 int ContentWidth{get{return Ui.Px(1088);}}
 public MainForm() {
  // Screenshots (Demo-Ansicht): kein Administrator-Hinweis, auch wenn Erstellen.bat erhöht läuft.
  bool elevated=!DemoMode&&IsElevated();
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); Text=AppInfo.Product+" "+AppInfo.Version+(elevated?" (Administrator)":"");
  AutoScaleMode=AutoScaleMode.None; Font=Ui.UiFont; BackColor=Ui.Bg; ForeColor=Ui.TextColor;
  StartPosition=FormStartPosition.CenterScreen;
  // Einstellungen aus dem alten Ordner (bis Version 2.12) einmalig übernehmen.
  string migrated=DemoMode?null:Store.MigrateOldFolder();
  // Screenshots: ohne gespeicherte Einstellungen (Vorgaben, Abstand 18, Navigation angepinnt).
  // Ab 2.52 stehen alle Programme in Positionen.json; beim ersten Start wird die Liste aus Startwerte.json
  // und den eigenen Programmen dort angelegt (vorher Sicherung der Datei).
  string programsMigrated=null,programsError=null;
  if(!DemoMode) {
   try {programsMigrated=Store.MigratePrograms();}
   catch(Exception ex) {programsError="Programmliste konnte nicht in Positionen.json übernommen werden – "+ex.Message;}
  }
  profiles=DemoMode?new Dictionary<string,object>():Store.Load();
  if(!DemoMode)activeArrangement=Store.ActiveName(Store.LoadRaw());
  string skipped=null,hiddenNames=null;
  LoadPrograms(out skipped,out hiddenNames);
  restore=new ModernButton("Wiederherstellen",ButtonKind.Primary,"\uE777");
  save=new ModernButton("Positionen speichern",ButtonKind.Secondary,"\uE74E");
  alignTop=new ModernButton("Oben",ButtonKind.Secondary,"\uE74A");
  alignBottom=new ModernButton("Unten",ButtonKind.Secondary,"\uE74B");
  alignLeft=new ModernButton("Links",ButtonKind.Secondary,"\uE72B");
  alignRight=new ModernButton("Rechts",ButtonKind.Secondary,"\uE72A");
  // Mitte ohne Symbolschrift: die Pfeile ↔ und ↕ stehen im Text.
  alignHCenter=new ModernButton("↔ Mitte",ButtonKind.Secondary,null);
  alignVCenter=new ModernButton("↕ Mitte",ButtonKind.Secondary,null);
  alignAuto=new ModernButton("Automatisch",ButtonKind.Secondary,"\uE740");
  all=new ModernButton("Alle auswählen",ButtonKind.Ghost,null);
  all.Height=Ui.Px(32);all.Margin=new Padding(0);
  addProgram=new ModernButton("Programm hinzufügen",ButtonKind.Ghost,"\uE710");
  addProgram.Height=Ui.Px(32);addProgram.Margin=new Padding(0);

  var header=new HeaderPanel{Dock=DockStyle.Fill,Title=AppInfo.Product,Subtitle="Fenster sichern, ausrichten und wiederherstellen",Badge="Version "+AppInfo.Version};
  header.Logo=LoadLogo(Ui.Px(48));
  signature=LoadSignature();
  header.BadgeClick+=(s,e)=>ShowInfo();
  footer.Dock=DockStyle.Fill;footer.Margin=new Padding(0);footer.Logo=signature;
  // Globale Richtlinie: links "© <Jahr> · <Programmname>".
  footer.Caption="© "+DateTime.Now.Year+" · "+AppInfo.Product;
  // Kopf und Fußzeile über die volle Breite, dazwischen Navigation und Inhalt.
  headerHost=new Panel{Dock=DockStyle.Top,Height=Ui.Px(18)+Ui.Px(76),Padding=new Padding(Ui.Px(24),Ui.Px(18),Ui.Px(24),Ui.Px(12)),BackColor=Ui.Bg};
  headerHost.Controls.Add(header);
  footerHost=new Panel{Dock=DockStyle.Bottom,Height=Ui.Px(66),Padding=new Padding(Ui.Px(24),0,Ui.Px(24),Ui.Px(2)),BackColor=Ui.Bg};
  footerHost.Controls.Add(footer);
  if(!elevated) {
   // Manche Programme (z.B. Easy2Sync) laufen mit Administratorrechten; ihre Fenster lassen sich nur mit gleichen Rechten verschieben.
   admin=new ModernButton("Als Administrator",ButtonKind.Secondary,"\uE7EF");
   admin.Margin=new Padding(0);
   admin.Click+=(s,e)=>RestartElevated();
   header.Controls.Add(admin);
  }
  header.Resize+=(s,e)=>{
   if(admin!=null)admin.Location=new Point(header.Width-Ui.Px(4)-admin.Width,(header.Height-admin.Height)/2);
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
   if(alignRules!=null)alignRules.Location=new Point(addProgram.Left-Ui.Px(4)-alignRules.Width,(programsHead.Height-alignRules.Height)/2);
  };
  programsHead.Resize+=layoutHead;all.SizeChanged+=layoutHead;addProgram.SizeChanged+=layoutHead;
  list.Dock=DockStyle.Fill;
  programsCard.Controls.Add(list);programsCard.Controls.Add(programsHead);list.BringToFront();

  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,Ui.Px(14)),Padding=new Padding(0),BackColor=Ui.Bg,WrapContents=false};
  var alignLabel=new Label{Text="Ausrichten",AutoSize=true,Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  alignLabel.Margin=new Padding(0,Math.Max(0,(Ui.Px(38)-alignLabel.PreferredHeight)/2),Ui.Px(10),0);
  // Anordnung: eigene Positionen und Kanten je Anordnung (z.B. "Arbeit" und "Spielen"), Auswahl über ein Menü.
  var arrangementLabel=new Label{Text="Anordnung",AutoSize=true,Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  arrangementLabel.Margin=new Padding(0,Math.Max(0,(Ui.Px(38)-arrangementLabel.PreferredHeight)/2),Ui.Px(10),0);
  arrangement=new ModernButton(activeArrangement,ButtonKind.Secondary,null){DropDown=true,Margin=new Padding(0)};
  arrangement.Click+=(s,e)=>ShowArrangementMenu();
  arrangementMenu.Opened+=(s,e)=>MenuStyle.RoundCorners(arrangementMenu);
  var arrangementSeparator=new ActionSeparator();
  actions.Controls.AddRange(new Control[]{restore,save,arrangementSeparator,arrangementLabel,arrangement});

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
  alignRules=new ModernButton("Kanten je Programm",ButtonKind.Ghost,"\uE9A6");
  // "Kanten je Programm" steht oben in der Kopfzeile der Programmliste (die Ausrichten-Zeile ist voll).
  alignRules.Height=Ui.Px(32);alignRules.Margin=new Padding(0);
  alignRules.Click+=(s,e)=>EditEdges();
  programsHead.Controls.Add(alignRules);
  alignRules.SizeChanged+=layoutHead;
  layoutHead(null,EventArgs.Empty);
  alignRow.Controls.AddRange(new Control[]{alignLabel,alignTop,alignBottom,alignLeft,alignRight,alignHCenter,alignVCenter,new ActionSeparator(),alignAuto,gapLabel,gapCard,pxLabel});
  alignTips.SetToolTip(alignRules,"Je Programm festlegen, welche Kanten ausgerichtet werden");
  alignTips.SetToolTip(alignTop,"Ausgewählte Fenster an die Oberkante des Arbeitsbereichs setzen");
  alignTips.SetToolTip(alignBottom,"Ausgewählte Fenster an die Unterkante des Arbeitsbereichs setzen (z.B. über die Taskleiste)");
  alignTips.SetToolTip(alignLeft,"Ausgewählte Fenster an den linken Rand setzen");
  alignTips.SetToolTip(alignRight,"Ausgewählte Fenster an den rechten Rand setzen");
  alignTips.SetToolTip(save,"Lage, Größe und Bildschirm der ausgewählten Fenster merken.\r\nNötig für alles, was unter „Kanten je Programm“ nicht angehakt ist (z.B. die Breite bei nur Oben/Unten),\r\nund für Programme ohne angehakte Kante.");
  alignTips.SetToolTip(restore,"Gespeicherte Lage, Größe und Bildschirm wiederherstellen,\r\ndanach die angehakten Kanten aus „Kanten je Programm“ ausrichten.");
  alignTips.SetToolTip(alignHCenter,"Ausgewählte Fenster waagerecht mittig setzen (gleicher Abstand links und rechts)");
  alignTips.SetToolTip(alignVCenter,"Ausgewählte Fenster senkrecht mittig setzen (gleicher Abstand oben und unten)");
  alignTips.SetToolTip(alignAuto,"Alle angehakten Kanten aus „Kanten je Programm“ auf einmal ausrichten\r\n(Oben + Unten = volle Höhe, Links + Rechts = volle Breite, ↔/↕ Mitte = mittig)");
  alignTips.SetToolTip(arrangement,"Anordnung wählen, neu anlegen, umbenennen oder löschen.\r\nJede Anordnung hat eigene Positionen und Kanten; Programmliste und Gruppen gelten für alle.");
  alignTips.SetToolTip(gapBox,"Abstand der sichtbaren Fensterkante zum Rand in Bildschirmpixeln (Standard 18)");

  hint.Dock=DockStyle.Fill;hint.Margin=new Padding(0,0,0,Ui.Px(14));
  // Drei kurze Zeilen, je ein Thema, damit nichts mitten im Satz umbricht.
  hint.Text="„Positionen speichern“ merkt sich Lage, Größe und Bildschirm; „Wiederherstellen“ setzt sie zurück und richtet danach die Kanten aus „Kanten je Programm“ aus.\r\n"+
   "Programme mit Einstellungsdatei oder Registry werden dafür beendet und neu gestartet; die übrigen bleiben offen.\r\n"+
   "Ziehen verschiebt ein Programm (auch in eine Gruppe); Rechtsklick: Gruppe, sortieren, ausblenden oder entfernen.";

  var logCard=new Card{Dock=DockStyle.Fill,Margin=new Padding(0),Padding=new Padding(Ui.Px(16),Ui.Px(6),Ui.Px(8),Ui.Px(12))};
  var logTitle=new Label{Text="Protokoll",Dock=DockStyle.Top,Height=Ui.Px(36),Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,TextAlign=ContentAlignment.MiddleLeft};
  log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BorderStyle=BorderStyle.None;
  log.BackColor=Ui.Surface;log.ForeColor=Ui.LogText;log.Font=Ui.MonoFont;
  log.HandleCreated+=(s,e)=>Native.DarkScroll(log);
  logCard.Controls.Add(log);logCard.Controls.Add(logTitle);log.BringToFront();

  int programsHeight=ProgramsHeight(ProgramList.CountLines(entries,CollapsedGroups()));
  // Kopf (18 + 76), Fußzeile (66) und die festen Zeilen der Startseite.
  int fixedHeight=Ui.Px(18)+Ui.Px(76)+Ui.Px(66)+Ui.Px(17)+Ui.Px(52)+Ui.Px(52)+Ui.Px(88);
  // Seitenrand 28 rechts und links, links gemessen ab dem Rand der Navigation (Globale Richtlinien).
  layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(Ui.Px(28),0,Ui.Px(28),0),ColumnCount=1,RowCount=7,BackColor=Ui.Bg};
  layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  // Zeile für den Update-Hinweis: Höhe 0, bis eine neuere Version gefunden wird.
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(17)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,programsHeight));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(52)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(52)));
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Ui.Px(88)));
  layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  updateBar.Dock=DockStyle.Fill;updateBar.Margin=new Padding(0,0,0,Ui.Px(10));updateBar.Visible=false;
  updateBar.Install.Click+=async(s,e)=>await InstallUpdate(false);
  updateBar.Page.Click+=(s,e)=>OpenReleasePage();
  updateBar.Later.Click+=(s,e)=>ShowUpdateBar(false);
  layout.Controls.Add(updateBar,0,0);layout.Controls.Add(activity,0,1);layout.Controls.Add(programsCard,0,2);
  layout.Controls.Add(actions,0,3);layout.Controls.Add(alignRow,0,4);layout.Controls.Add(hint,0,5);layout.Controls.Add(logCard,0,6);
  BuildNavigation();
  // Andockreihenfolge: Kopf und Fußzeile über die volle Breite, dann Navigation, dann Inhalt bzw. Seite.
  Controls.Add(viewHost);
  Controls.Add(layout);
  Controls.Add(navSpacer);
  Controls.Add(footerHost);
  Controls.Add(headerHost);
  Controls.Add(nav); // nicht angedockt: liegt ausgeklappt über dem Inhalt
  nav.BringToFront();

  foreach(string group in CollapsedGroups())list.Collapsed.Add(group);
  foreach(var e in entries) list.Add(new ProgramRow{Entry=e,Name=e.Name,Checked=false}); // Beim Start ist nichts angehakt.
  SyncEntries();
  list.CheckedChanged+=(s,e)=>UpdateSelection();
  list.RowMenu+=ShowRowMenu;
  list.RowsReordered+=(s,e)=>SaveOrder(list.Rows.Select(x=>x.Entry).ToList(),null);
  list.GroupsChanged+=(s,e)=>{SaveCollapsed();UpdateProgramsHeight();};
  rowMenu.Opened+=(s,e)=>MenuStyle.RoundCorners(rowMenu);
  UpdateSelection();

  Rectangle work=Screen.PrimaryScreen.WorkingArea;
  ClientSize=new Size(NavSpace()+ContentWidth,fixedHeight+programsHeight+Ui.Px(230));
  int chrome=Height-ClientSize.Height;
  // Mindestbreite: Navigation + Inhalt (sonst überlappt die Ausrichten-Zeile); wird in ApplyNav nachgeführt.
  MinimumSize=new Size(Math.Min(Width,work.Width),Math.Min(fixedHeight+programsHeight+Ui.Px(120)+chrome,work.Height));
  if(Height>work.Height)Height=work.Height;
  if(Width>work.Width)Width=work.Width;
  Resize+=(s,e)=>ApplyNav();
  footerHost.LocationChanged+=(s,e)=>ApplyNav();
  headerHost.SizeChanged+=(s,e)=>ApplyNav();
  ApplyNav();

  all.Click+=(s,e)=>{bool select=list.Rows.Any(x=>!x.Checked);foreach(var row in list.Rows)row.Checked=select;UpdateSelection();};
  restore.Click+=async(s,e)=>await Run(false);
  save.Click+=async(s,e)=>await Run(true);
  alignTop.Click+=async(s,e)=>await AlignWindows("top");
  alignBottom.Click+=async(s,e)=>await AlignWindows("bottom");
  alignLeft.Click+=async(s,e)=>await AlignWindows("left");
  alignRight.Click+=async(s,e)=>await AlignWindows("right");
  alignHCenter.Click+=async(s,e)=>await AlignWindows("hcenter");
  alignVCenter.Click+=async(s,e)=>await AlignWindows("vcenter");
  alignAuto.Click+=async(s,e)=>await AlignWindows("auto");
  addProgram.Click+=(s,e)=>AddProgram();
  FormClosing+=(s,e)=>{if(busy||!ConfirmLeaveView())e.Cancel=true;};
  FormClosed+=(s,e)=>{navTimer.Stop();navHover.Stop();};
  ApplyTheme();
  Log(AppInfo.Product+" Version "+AppInfo.Version+" – Farbschema: "+(Ui.Dark?"dunkel":"hell")+", Akzentfarbe "+Ui.Hex(Ui.AccentBase));
  TraceLog.Cleanup(30);
  if(migrated!=null)Log(migrated);
  if(programsMigrated!=null)Log(programsMigrated);
  if(Store.StartNote!=null)Log(Store.StartNote);
  if(programsError!=null)Log(programsError);
  Log("Positionen: "+Store.Display(Store.Profiles)+(activeArrangement!=Store.StandardName?" – Anordnung „"+activeArrangement+"“":""));
  Log("Protokolldatei: "+Store.Display(TraceLog.Today));
  if(skipped!=null)Log("Nicht installiert (nicht angezeigt): "+skipped);
  if(hiddenNames!=null)Log("Ausgeblendet: "+hiddenNames+" – Rechtsklick in die Liste zeigt sie wieder an.");
  if(AfterUpdate) {
   // Ergebnis des Update-Skripts: OK, ZURUECK (vorherige Version wiederhergestellt), sonst nicht installiert.
   // Meldung ohne Pfade und Technik; der Originaltext steht nur in der Protokolldatei.
   string result=Updater.LastResult(UpdateAttemptId);
   if(result!=null&&result.StartsWith("OK"))Log("Update auf Version "+result.Substring(2).Trim()+" installiert.");
   else if(result!=null&&result.StartsWith("ZURUECK"))Log("Vorherige Version wiederhergestellt – das Update kann jetzt erneut installiert werden.");
   else if(result!=null) {
    TraceLog.Write("Update nicht installiert: "+result);
    Log("Update nicht installiert – die bisherige Version läuft weiter. Einzelheiten stehen in der Protokolldatei.");
   }
  }
  // Update-Prüfung still ca. 15 s nach jedem Start (Globale Richtlinien, Updates; abschaltbar im Info-Fenster).
  // Beim Wiederherstellen per Verknüpfung nicht: Das Programm beendet sich dort nach wenigen Sekunden.
  updateTimer.Tick+=async(s,e)=>{updateTimer.Stop();await CheckForUpdates(false,true);};
  autoUpdateRetry.Tick+=(s,e)=>{autoUpdateRetry.Stop();TryAutoUpdate();};
  FormClosed+=(s,e)=>{updateTimer.Stop();autoUpdateRetry.Stop();};
  if(AutoRestore) {
   // Per Verknüpfung: minimiert wiederherstellen; bei Erfolg beendet sich das Programm, sonst zeigt es das Protokoll.
   WindowState=FormWindowState.Minimized;
   Shown+=async(s,e)=>await RunAutoRestore();
  } else Shown+=(s,e)=>{if(!DemoMode)updateTimer.Start();};
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
   if(control is Label) {
    string tag=control.Tag as string;
    control.ForeColor=tag=="muted"?Ui.Muted:tag=="accent"?Ui.Accent:tag=="error"?Ui.ErrorColor:tag=="warning"?Ui.WarningColor:tag=="success"?Ui.SuccessColor:Ui.TextColor;
   }
   if(control is TextBox)control.ForeColor=Ui.TextColor;
   ApplyTheme(control,card);
  }
 }
 // ================================================================== Navigation
 // Navigation links (Globale Richtlinien, Navigation; Referenz: Wake on LAN, MainForm.ApplyNav/LayoutNav):
 // ausgeklappt 230, eingeklappt 68 (nur Symbole); oben ☰ und die Stecknadel, unten "Einstellungen" (Menü).
 // "Beenden" gibt es nicht: Fensterpositionen läuft nicht im Infobereich, das X beendet das Programm.
 void BuildNavigation() {
  navSpacer=new Panel{Dock=DockStyle.Left,BackColor=Ui.Bg};
  nav=new Panel{BackColor=Ui.Bg};
  nav.Paint+=(s,e)=>{
   // Ausgeklappt über dem Inhalt: feine Trennlinie rechts.
   if(navOpen&&!NavPinned())using(var pen=new Pen(Ui.Border))e.Graphics.DrawLine(pen,nav.Width-1,0,nav.Width-1,nav.Height);
  };
  navMenu=AddNav("",'\uE700',()=>{
   if(NavPinned())SetNavPinned(false);
   else {navOpen=!navOpen;ApplyNav();}
  });
  navMenu.AccessibleName="Navigation aus- bzw. einklappen";
  alignTips.SetToolTip(navMenu,"Navigation aus- bzw. einklappen");
  navPin=AddNav("",'\uE718',()=>SetNavPinned(!NavPinned()));
  navPin.IconOnly=true;
  navPrograms=AddNav("Programme",'\uE8FD',()=>{CloseNav();if(ConfirmLeaveView())CloseView();});
  navPrograms.Selected=true;
  navEdges=AddNav("Kanten je Programm",'\uE9A6',EditEdges);
  navOneTime=AddNav("Einmalig ausrichten",'\uE7C9',AlignOneWindow);
  alignTips.SetToolTip(navOneTime,"Ein beliebiges offenes Fenster anklicken und einmalig ausrichten\r\n(wird nicht in die Liste aufgenommen, nichts wird gespeichert)");
  navMore=AddNav("Einstellungen",'\uE713',ShowSettingsMenu);
  nav.Resize+=(s,e)=>LayoutNav();
  navTimer.Tick+=(s,e)=>{
   // Ausgeklappt, nicht angepinnt: wieder einklappen, sobald die Maus die Navigation verlässt.
   // Solange das Menü "Einstellungen" offen ist, bleibt sie ausgeklappt.
   if(!navOpen||NavPinned()||nav.Capture||!Visible||settingsMenu.Visible)return;
   if(!nav.RectangleToScreen(nav.ClientRectangle).Contains(Cursor.Position)){navOpen=false;ApplyNav();}
  };
  navTimer.Start();
  // Nicht angepinnt: nach ca. 400 ms Verweilen auf der eingeklappten Navigation ausklappen (Globale Richtlinien,
  // Navigation links; Vorlage: Wake on LAN, HookNavHover). Kurzes Überfahren löst nichts aus.
  navHover.Tick+=(s,e)=>{
   navHover.Stop();
   if(NavPinned()||navOpen||!Visible||!OverNav())return;
   navOpen=true;
   ApplyNav();
  };
  HookNavHover(nav);
  // Seiten: Überschrift wie Wake on LAN (Segoe UI Semibold 20 pt), darunter das eingebettete Fenster.
  viewTitle=new Label{Dock=DockStyle.Top,Height=Ui.Px(56),Font=new Font("Segoe UI Semibold",20f),ForeColor=Ui.TextColor,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleLeft,UseMnemonic=false};
  viewBody=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Bg,AutoScroll=true};
  // Die eingebetteten Fenster haben selbst 24 Rand: Überschrift und Inhalt beginnen so beide bei 28.
  viewHost=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Bg,Visible=false,Padding=new Padding(Ui.Px(4),0,Ui.Px(4),Ui.Px(4))};
  viewTitle.Padding=new Padding(Ui.Px(24),0,0,0);
  viewHost.Controls.Add(viewBody);
  viewHost.Controls.Add(viewTitle);
 }
 readonly System.Windows.Forms.Timer navHover=new System.Windows.Forms.Timer{Interval=400};
 bool OverNav(){return nav.RectangleToScreen(nav.ClientRectangle).Contains(Cursor.Position);}
 void HookNavHover(Control c) {
  c.MouseEnter+=(s,e)=>{if(!NavPinned()&&!navOpen&&!navHover.Enabled)navHover.Start();};
  // Wechsel zwischen den Einträgen zählt nicht als Verlassen.
  c.MouseLeave+=(s,e)=>{if(!OverNav())navHover.Stop();};
  foreach(Control child in c.Controls)HookNavHover(child);
 }
 NavButton AddNav(string text,char glyph,Action click) {
  var button=new NavButton(text,glyph);
  button.Click+=(s,e)=>click();
  nav.Controls.Add(button);
  return button;
 }
 // Angepinnt (Standard) oder einklappbar; gespeichert in Positionen.json ("_nav": pinned).
 bool NavPinned() {
  if(DemoMode)return true;
  var saved=profiles!=null&&profiles.ContainsKey("_nav")?profiles["_nav"] as Dictionary<string,object>:null;
  try {return saved==null||!saved.ContainsKey("pinned")||Convert.ToBoolean(saved["pinned"]);} catch {return true;}
 }
 int NavSpace(){return NavPinned()?Ui.Px(230):Ui.Px(68);}
 void SetNavPinned(bool pinned) {
  if(!DemoMode) {
   try {
    var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
    copy["_nav"]=new Dictionary<string,object>{{"pinned",pinned}};
    Store.SaveProfiles(copy);
    profiles=copy;
   } catch(Exception ex) {Log("Navigation: Einstellung konnte nicht gespeichert werden – "+ex.Message);}
  }
  navOpen=pinned;
  ApplyNav();
 }
 // Ausgeklappte (nicht angepinnte) Navigation nach einer Auswahl wieder einklappen.
 void CloseNav(){if(!NavPinned()&&navOpen){navOpen=false;ApplyNav();}}
 void ApplyNav() {
  if(nav==null||footerHost==null||headerHost==null)return;
  bool pinned=NavPinned();
  bool open=pinned||navOpen;
  int full=Ui.Px(230),compact=Ui.Px(68);
  int space=pinned?full:compact;
  if(navSpacer.Width!=space) {
   navSpacer.Width=space;
   // Mindestbreite mitführen: Angepinnt braucht das Fenster die Navigation zusätzlich zum Inhalt.
   Rectangle work=Screen.FromControl(this).WorkingArea;
   int min=Math.Min(space+ContentWidth+(Width-ClientSize.Width),work.Width);
   MinimumSize=new Size(min,MinimumSize.Height);
   if(WindowState==FormWindowState.Normal&&Right>work.Right)Left=Math.Max(work.Left,work.Right-Width);
  }
  int top=headerHost.Bottom,h=Math.Max(0,footerHost.Top-headerHost.Bottom);
  nav.SetBounds(0,top,open?full:compact,h);
  nav.BringToFront();
  navPin.Visible=open;
  navPin.Glyph=pinned?'\uE77A':'\uE718';
  string pinText=pinned?"Navigation lösen (einklappbar)":"Navigation anpinnen";
  navPin.AccessibleName=pinText;
  alignTips.SetToolTip(navPin,pinText);
  nav.Invalidate();
  LayoutNav();
 }
 void LayoutNav() {
  if(navMore==null)return;
  int x=Ui.Px(10),w=nav.Width-Ui.Px(20),h=navPrograms.Height;
  int y=Ui.Px(4);
  navMenu.SetBounds(x,y,Ui.Px(48),h);
  navPin.SetBounds(nav.Width-Ui.Px(10)-Ui.Px(36),y+(h-Ui.Px(36))/2,Ui.Px(36),Ui.Px(36));
  y+=h+Ui.Px(10);
  foreach(var b in new[]{navPrograms,navEdges,navOneTime}) {
   b.SetBounds(x,y,w,h);
   y+=h+Ui.Px(4);
  }
  int bottom=nav.Height-Ui.Px(14);
  navMore.SetBounds(x,bottom-h,w,h);
 }
 // Einstellungen (Zahnrad): Menü rechts über dem Knopf.
 readonly ContextMenuStrip settingsMenu=new ContextMenuStrip();
 void ShowSettingsMenu() {
  NavButton current=viewTitle.Tag as NavButton??navPrograms;
  current.Selected=false;
  navMore.Selected=true; // ausgewählt, solange das Menü offen ist
  MenuStyle.Prepare(settingsMenu);
  settingsMenu.Items.Add(MenuStyle.TopSpacer());
  settingsMenu.Items.Add(MenuStyle.Item("Sicherungen öffnen",'\uE838',(s,a)=>OpenBackups(s,a)));
  settingsMenu.Items.Add(MenuStyle.Item("Protokolle öffnen",'\uE81C',(s,a)=>OpenFolder(TraceLog.Folder)));
  string guide=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Anleitung.txt");
  var guideItem=MenuStyle.Item("Anleitung öffnen",'\uE8A5',(s,a)=>{try{Process.Start(guide);}catch(Exception ex){MessageBox.Show(this,ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);}});
  guideItem.Enabled=System.IO.File.Exists(guide);
  settingsMenu.Items.Add(guideItem);
  var hidden=HiddenPrograms();
  if(hidden.Count>0) {
   settingsMenu.Items.Add(new ToolStripSeparator());
   var showHidden=MenuStyle.Item("Ausgeblendete wieder anzeigen ("+hidden.Count+")",'\uE890',(s,a)=>ShowHiddenPrograms());
   showHidden.Enabled=!busy;
   settingsMenu.Items.Add(showHidden);
  }
  settingsMenu.Items.Add(new ToolStripSeparator());
  var export=MenuStyle.Item("Einstellungen exportieren …",'\uEDE1',(s,a)=>BeginInvoke((Action)ExportSettings));
  var import=MenuStyle.Item("Einstellungen importieren …",'\uE8B5',(s,a)=>BeginInvoke((Action)ImportSettings));
  export.Enabled=!DemoMode;import.Enabled=!busy&&!DemoMode;
  settingsMenu.Items.Add(export);
  settingsMenu.Items.Add(import);
  settingsMenu.Items.Add(new ToolStripSeparator());
  settingsMenu.Items.Add(MenuStyle.Item("Info über "+AppInfo.Product,'\uE946',(s,a)=>BeginInvoke((Action)ShowInfo)));
  settingsMenu.Items.Add(MenuStyle.BottomSpacer());
  ToolStripDropDownClosedEventHandler closed=null;
  closed=(s,e)=>{
   settingsMenu.Closed-=closed;
   navMore.Selected=false;
   if(view==null)navPrograms.Selected=true;
   else current.Selected=viewTitle.Tag==current;
  };
  settingsMenu.Closed+=closed;
  settingsMenu.Opened-=SettingsMenuOpened;
  settingsMenu.Opened+=SettingsMenuOpened;
  MenuStyle.ShowOnScreen(settingsMenu,navMore,new Point(navMore.Width+Ui.Px(4),navMore.Height),ToolStripDropDownDirection.AboveRight);
 }
 void SettingsMenuOpened(object sender,EventArgs e){MenuStyle.RoundCorners(settingsMenu);}
 void OpenFolder(string path) {
  try {Directory.CreateDirectory(path);Process.Start(Store.ExplorerExe,Quote(path));}
  catch(Exception ex) {MessageBox.Show(this,ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }

 // ================================================================== Seiten im selben Fenster
 // Zeigt ein bisheriges Dialogfenster als Seite im Inhaltsbereich (ohne Rahmen, eingebettet).
 // "Abbrechen" bzw. "Speichern" führen zurück zu "Programme"; done erhält das Ergebnis.
 void ShowView(NavButton item,Form f,Action<DialogResult> done,string title) {
  if(title==null)title=item.Text;
  CloseNav();
  if(view!=null&&viewTitle.Tag==item&&viewTitle.Text==title){f.Dispose();return;} // schon offen
  if(!ConfirmLeaveView()){f.Dispose();return;}
  CloseView();
  var size=f.ClientSize;
  f.TopLevel=false;
  f.FormBorderStyle=FormBorderStyle.None;
  f.ShowInTaskbar=false;
  f.ClientSize=size;
  // Die Seite füllt den Inhaltsbereich (volle Breite); kleiner als ihre Startbreite wird sie nicht,
  // in der Höhe darf sie bis 480 schrumpfen (darunter scrollt der Bereich).
  f.MinimumSize=new Size(size.Width,Math.Min(size.Height,Ui.Px(480)));
  f.Dock=DockStyle.Fill;
  // Knöpfe mit DialogResult schließen ein eingebettetes Fenster nicht von selbst.
  foreach(var b in AllButtons(f)) {
   var button=b;
   if(button.DialogResult!=DialogResult.None)
    button.Click+=(s,e)=>{f.DialogResult=button.DialogResult;BeginInvoke((Action)f.Close);};
  }
  f.FormClosed+=(s,e)=>BeginInvoke((Action)(()=>EndView(f)));
  view=f;
  viewDone=done;
  viewTitle.Text=title;
  viewTitle.Tag=item;
  foreach(var n in new[]{navPrograms,navEdges,navOneTime})n.Selected=n==item;
  layout.Visible=false;
  viewBody.Controls.Add(f);
  viewHost.Visible=true;
  viewHost.BringToFront();
  nav.BringToFront();
  f.Show();
  f.Focus();
 }
 // Ungespeicherte Änderungen auf einer Einstellungsseite: vor dem Verlassen (Navigation, Fenster schließen)
 // nachfragen. Vorauswahl "Nein" = auf der Seite bleiben. "Abbrechen" auf der Seite verwirft ohne Rückfrage.
 bool ConfirmLeaveView() {
  var page=view as AlignRulesForm;
  if(page==null||!page.HasChanges)return true;
  return MessageBox.Show(this,"Die Änderungen auf der Seite „"+viewTitle.Text+"“ sind noch nicht gespeichert.\r\n\r\nÄnderungen verwerfen?","Nicht gespeichert",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes;
 }
 static IEnumerable<Button> AllButtons(Control c) {
  foreach(Control child in c.Controls) {
   var b=child as Button;
   if(b!=null)yield return b;
   foreach(var inner in AllButtons(child))yield return inner;
  }
 }
 // Offene Seite schließen (wie "Abbrechen") und zurück zu "Programme".
 void CloseView() {
  var f=view;
  if(f==null)return;
  f.DialogResult=DialogResult.Cancel;
  f.Close();
  EndView(f);
 }
 void EndView(Form f) {
  if(view!=f)return;
  var done=viewDone;
  var result=f.DialogResult;
  view=null;viewDone=null;
  viewTitle.Tag=null;
  viewBody.Controls.Remove(f);
  if(!f.IsDisposed)f.Dispose();
  viewHost.Visible=false;
  layout.Visible=true;
  foreach(var n in new[]{navEdges,navOneTime})n.Selected=false;
  navPrograms.Selected=true;
  nav.BringToFront();
  if(done!=null)done(result);
 }

 // Das Logo kommt aus der eingebetteten ICO-Datei (Erstellen.bat) oder aus Resources\Fensterpositionen.ico.
 // Die ICO enthält PNG-Bilder; Icon.ToBitmap liest diese nicht zuverlässig, daher eigener Leser.
 void OpenBackups(object sender,EventArgs e){string p=Path.Combine(Store.Root,"Sicherungen");Directory.CreateDirectory(p);Process.Start(Store.ExplorerExe,Quote(p));}
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
  var hooks=new UpdateHooks{
   Status=()=>CurrentUpdateStatus(),
   Auto=()=>UpdateAuto(),
   SetAuto=on=>SaveUpdateSettings(on,null,null),
   CheckNow=()=>CheckForUpdates(true,false),
   AutoInstall=()=>UpdateAutoInstall(),
   SetAutoInstall=on=>{
    SaveUpdateSettings(null,null,on);
    // Schon eine neue Version bekannt: gleich versuchen (erst nach dem Schließen des Info-Fensters).
    if(on&&available!=null)BeginInvoke((Action)TryAutoUpdate);
   },
   CanInstall=()=>Updater.CanInstall(available)||(available!=null&&Updater.RepairPending),
   Install=()=>BeginInvoke((Action)(async()=>await InstallUpdate(false))),
   OpenPage=()=>OpenReleasePage()
  };
  using(var about=new AboutForm(signature,Icon,OpenBackups,hooks)) about.ShowDialog(this);
  // "Automatisch aktualisieren" gerade eingeschaltet und eine neue Version ist bekannt: jetzt versuchen.
  if(available!=null&&UpdateAutoInstall())TryAutoUpdate();
 }
 // Update-Einstellungen in Positionen.json ("_update": auto = automatisch prüfen, autoinstall = automatisch
 // aktualisieren (Standard aus), last = letzte Prüfung in UTC, ISO 8601).
 Dictionary<string,object> UpdateSettings() {
  var saved=profiles!=null&&profiles.ContainsKey("_update")?profiles["_update"] as Dictionary<string,object>:null;
  return saved??new Dictionary<string,object>();
 }
 bool UpdateAuto() {
  var saved=UpdateSettings();
  try {return !saved.ContainsKey("auto")||Convert.ToBoolean(saved["auto"]);} catch {return true;}
 }
 bool UpdateAutoInstall() {
  var saved=UpdateSettings();
  try {return saved.ContainsKey("autoinstall")&&Convert.ToBoolean(saved["autoinstall"]);} catch {return false;}
 }
 DateTime UpdateLast() {
  var saved=UpdateSettings();
  DateTime last;
  if(saved.ContainsKey("last")&&DateTime.TryParse(Convert.ToString(saved["last"]),System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out last))return last;
  return DateTime.MinValue;
 }
 void SaveUpdateSettings(bool? auto,DateTime? last,bool? autoInstall) {
  if(DemoMode)return;
  try {
   var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
   var settings=copy.ContainsKey("_update")?copy["_update"] as Dictionary<string,object>:null;
   if(settings==null){settings=new Dictionary<string,object>();copy["_update"]=settings;}
   if(auto.HasValue)settings["auto"]=auto.Value;
   if(last.HasValue)settings["last"]=last.Value.ToString("o",System.Globalization.CultureInfo.InvariantCulture);
   if(autoInstall.HasValue)settings["autoinstall"]=autoInstall.Value;
   Store.SaveProfiles(copy);
   profiles=copy;
  } catch(Exception ex) {Log("Update-Einstellung konnte nicht gespeichert werden: "+ex.Message);}
 }
 UpdateStatus CurrentUpdateStatus() {
  DateTime last=UpdateLast();
  string checkedText=last==DateTime.MinValue?"":"Zuletzt geprüft "+last.ToLocalTime().ToString("dd.MM.yyyy");
  // Fehler (auch beim automatischen Installieren) vor "Version x verfügbar": Kurzmeldung mit "Details".
  if(updateError!=null&&updateError.State==UpdateState.Error)return updateError;
  // Ohne gültige Signatur: "Version x verfügbar" mit Kurzmeldung ("Nicht signiert." usw.) und "Details".
  if(available!=null&&available.Problem!=null)return new UpdateStatus{State=UpdateState.Available,Text="Version "+available.Version+" verfügbar",Detail=available.Problem,More=available.ProblemDetail??""};
  if(available!=null)return new UpdateStatus{State=UpdateState.Available,Text="Version "+available.Version+" verfügbar",Detail=checkedText};
  if(updateError!=null)return updateError;
  if(last==DateTime.MinValue)return new UpdateStatus{State=UpdateState.Unknown,Text="Noch nicht geprüft",Detail=""};
  return new UpdateStatus{State=UpdateState.Current,Text="Aktuell",Detail=checkedText};
 }
 // Neueste Version auf GitHub abfragen. Automatisch (manual=false) still ca. 15 s nach jedem Start, ohne
 // 20-Stunden-Sperre. Das Ergebnis einer Prüfung von Hand zeigt nur das Info-Fenster; im Hauptfenster erscheint
 // nur bei einer neuen Version die Hinweisleiste. Fehler stehen im Info-Fenster und in der Protokolldatei.
 async Task<UpdateStatus> CheckForUpdates(bool manual,bool atStart) {
  if(DemoMode||updateRunning)return CurrentUpdateStatus();
  if(!manual&&(!UpdateAuto()||(!atStart&&(DateTime.UtcNow-UpdateLast().ToUniversalTime()).TotalHours<20)))return CurrentUpdateStatus();
  updateRunning=true;
  try {
   // Reste unterbrochener Updates, bei denen nichts zurückzusetzen ist, still aufräumen (nur unter der Sperre)
   var release=await Task.Run(()=>{Updater.QuietCleanup();return Updater.GetLatest();});
   SaveUpdateSettings(null,DateTime.UtcNow,null);
   updateError=null;
   if(Updater.IsNewer(release.Version)) {
    bool fresh=available==null||available.Version!=release.Version;
    available=release;
    if(fresh)Log("Neue Version "+release.Version+" auf GitHub verfügbar (installiert: "+AppInfo.Version+").");
    ShowUpdateBar(true);
    // "Automatisch aktualisieren": ohne Rückfrage installieren, sobald nichts mehr läuft (nicht bei "Jetzt prüfen").
    if(!manual&&UpdateAutoInstall()&&Updater.CanInstall(release))BeginInvoke((Action)TryAutoUpdate);
   } else {
    available=null;
    ShowUpdateBar(false);
   }
  } catch(Exception ex) {
   var st=UpdateStatus.FromError(ex);
   updateError=st;
   // "Nur Vorabversionen" ist kein Fehler: gilt als geprüft.
   if(st.State!=UpdateState.Error){available=null;SaveUpdateSettings(null,DateTime.UtcNow,null);}
   else TraceLog.Write("Update-Prüfung nicht möglich: "+ex.Message+(st.More.Length>0?" – "+st.More.Replace("\r\n"," "):""));
  } finally {updateRunning=false;}
  return CurrentUpdateStatus();
 }
 // Beschäftigt: ein Ablauf läuft, die offene Seite hat ungespeicherte Änderungen, ein Dialog ist offen oder
 // es wird gerade per Verknüpfung wiederhergestellt. Dann nicht automatisch aktualisieren (Neustart würde stören).
 bool BusyForUpdate() {
  if(busy||AutoRestore)return true;
  var page=view as AlignRulesForm;
  if(page!=null&&!page.IsDisposed&&page.HasChanges)return true;
  foreach(Form f in Application.OpenForms)if(f.Visible&&f.Modal)return true;
  return false;
 }
 // "Automatisch aktualisieren": installieren, wenn nichts läuft – sonst alle 5 Minuten erneut versuchen.
 void TryAutoUpdate() {
  if(available==null||installing||DemoMode||!UpdateAutoInstall()||!UpdateAuto())return;
  if(!Updater.CanInstall(available)||Updater.Busy)return;
  if(BusyForUpdate()){autoUpdateRetry.Stop();autoUpdateRetry.Start();return;}
  BeginInvoke((Action)(async()=>await InstallUpdate(true)));
 }
 void ShowUpdateBar(bool show) {
  show=show&&available!=null;
  if(show==updateBar.Visible)return;
  int height=Ui.Px(64);
  if(show) {
   updateBar.Text="Neue Version "+available.Version+" verfügbar – installiert ist "+AppInfo.Version+".";
   // Im Projektordner, ohne gültige Signatur oder mit Adminrechten öffnet "Jetzt aktualisieren" die Release-Seite;
   // bei einem nicht abgeschlossenen Update bietet es die Wiederherstellung an.
   string blocker=Updater.InstallBlocker();
   if(blocker==null&&available.Problem!=null)blocker=AppInfo.Product+" installiert dieses Update nicht: "+available.Problem+" "+available.ProblemDetail;
   bool repair=blocker==Updater.PendingText;
   string tip=repair?blocker+"\r\nEin Klick stellt die vorherige Version wieder her.":
    blocker!=null?blocker+"\r\nKlick öffnet stattdessen die Release-Seite.":
    String.IsNullOrEmpty(available.ZipUrl)?"Im Release ist keine passende ZIP-Datei.\r\nKlick öffnet die Release-Seite.":
    "Signiertes Update laden, Programm beenden, EXE und Anleitung ersetzen und neu starten.\r\nGespeicherte Positionen und Einstellungen bleiben erhalten.";
   alignTips.SetToolTip(updateBar.Install,tip);
   alignTips.SetToolTip(updateBar,String.IsNullOrEmpty(available.Notes)?"Die Änderungen stehen auf der Release-Seite.":"Änderungen:\r\n"+available.Notes);
  }
  // Fenster um die Hinweiszeile vergrößern bzw. verkleinern, soweit der Bildschirm reicht.
  Rectangle work=Screen.FromControl(this).WorkingArea;
  layout.RowStyles[UpdateRow].Height=show?height:0;
  updateBar.Visible=show;
  if(WindowState==FormWindowState.Normal) {
   if(show&&Bottom+height<=work.Bottom)Height+=height;
   else if(!show&&Height-height>=MinimumSize.Height)Height-=height;
  }
 }
 void OpenReleasePage() {
  try {Process.Start(available!=null&&!String.IsNullOrEmpty(available.PageUrl)?available.PageUrl:Updater.ReleasesPage);}
  catch(Exception ex){MessageBox.Show(this,ex.Message);}
 }
 // automatic = "Automatisch aktualisieren": ohne Rückfrage, Fehler ohne Meldung (Hinweisleiste bleibt,
 // Kurzmeldung mit "Details" im Info-Fenster).
 async Task InstallUpdate(bool automatic) {
  if(busy||installing||available==null)return;
  var info=available;
  if(!automatic&&Updater.Busy) {
   MessageBox.Show(this,"Ein Update läuft bereits.","Update",MessageBoxButtons.OK,MessageBoxIcon.Information);
   return;
  }
  Updater.QuietCleanup();
  if(!automatic&&Updater.RepairPending) {
   // Ein früheres Update wurde nicht abgeschlossen: erst die vorherige Version wiederherstellen.
   if(Updater.OfferRepair(this,false)) {
    Log("Vorherige Version wird wiederhergestellt – Fensterpositionen startet gleich neu.");
    busy=false;
    Close();
   }
   return;
  }
  if(!Updater.CanInstall(info)) {
   if(automatic)return;
   string why=Updater.InstallBlocker()??(info.Problem!=null?AppInfo.Product+" installiert dieses Update nicht: "+info.Problem+
    (String.IsNullOrEmpty(info.ProblemDetail)?"":"\r\n\r\n"+info.ProblemDetail):"Im Release ist keine passende ZIP-Datei.");
   if(MessageBox.Show(this,why+"\r\n\r\nRelease-Seite auf GitHub öffnen?","Update",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)OpenReleasePage();
   return;
  }
  if(!automatic&&MessageBox.Show(this,AppInfo.Product+" wird beendet, auf Version "+info.Version+" aktualisiert und neu gestartet. Einstellungen bleiben erhalten.\r\n\r\nJetzt aktualisieren?",
   "Update",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  installing=true;
  SetBusy(true);
  foreach(var b in new[]{updateBar.Install,updateBar.Page,updateBar.Later})b.Enabled=false;
  string text=updateBar.Text;
  try {
   Log("Lade Version "+info.Version+" …");
   await Updater.Install(info,percent=>BeginInvoke((Action)(()=>updateBar.Text="Lade Version "+info.Version+" … "+percent+" %")),false);
   Log("Update wird "+(automatic?"automatisch ":"")+"installiert – Fensterpositionen startet gleich neu.");
   busy=false;
   Close();
  } catch(Exception ex) {
   installing=false;
   Log((automatic?"Automatisches Update":"Update")+" fehlgeschlagen: "+ex.Message);
   if(automatic)updateError=UpdateStatus.FromError(ex);
   else MessageBox.Show(this,ex.Message,"Update",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   updateBar.Text=text;
   SetBusy(false);
   foreach(var b in new[]{updateBar.Page,updateBar.Later})b.Enabled=true;
   updateBar.Install.Enabled=true;
  }
 }
 // Eingebettete Ressource suchen; der Name kann einen Ordner enthalten (z.B. "Resources\Fensterpositionen.ico").
 static Stream ResourceStream(string name) {
  var assembly=Assembly.GetExecutingAssembly();
  foreach(string resource in assembly.GetManifestResourceNames())
   if(resource.Equals(name,StringComparison.OrdinalIgnoreCase)||resource.EndsWith("\\"+name,StringComparison.OrdinalIgnoreCase)||resource.EndsWith("/"+name,StringComparison.OrdinalIgnoreCase))
    return assembly.GetManifestResourceStream(resource);
  return null;
 }
 // Datei im Ordner Resources neben der EXE, sonst direkt neben der EXE. Nur wenn der Pfad lokal ist und über keine
 // Umleitung führt (Store.SafeToTouch); sonst null, dann gilt das eingebettete Bild bzw. keines.
 static string ProjectFile(string name) {
  bool missing;
  string inResources=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Resources",name);
  if(Store.SafeToTouch(inResources,out missing)&&!missing)return inResources;
  string beside=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);
  return Store.SafeToTouch(beside,out missing)&&!missing?beside:null;
 }
 // Signatur-Logo: eingebettet (Erstellen.bat) oder Datei GS-Logo.png neben der EXE.
 internal static Bitmap LoadSignature() {
  try {
   using(var stream=ResourceStream("GS-Logo.png")) {
    if(stream!=null)using(var image=Image.FromStream(stream))return new Bitmap(image);
   }
   string file=ProjectFile("GS-Logo.png");
   if(file!=null&&System.IO.File.Exists(file))using(var image=Image.FromFile(file))return new Bitmap(image);
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
    if(ico!=null&&System.IO.File.Exists(ico))data=System.IO.File.ReadAllBytes(ico);
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
  var demo=new Entry{Id="eigen-notepad",Name="Editor",ProcessName="notepad",Exe=@"C:\Windows\System32\notepad.exe",WindowOnly=true,Keys=new[]{"Window"}};
  entries.Add(demo);
  list.Add(new ProgramRow{Entry=demo,Name=demo.Name,Checked=false});
  UpdateProgramsHeight();
  var states=new Dictionary<string,string>{
   {"taskmanager","Unten: 18 px"},{"dumeter","Ausrichten übersprungen"},
   {"discord","Unten: 18 px"},{"gpuz","Unten: 18 px"}};
  foreach(var row in list.Rows) {
   string status;
   if(states.TryGetValue(row.Entry.Id,out status)){row.Checked=true;row.Status=status;}
   else if(row.Entry.Id==demo.Id)row.Status="Position gespeichert";
  }
  UpdateSelection();
  Log("Programm hinzugefügt: Editor (notepad.exe)");
  Log("Editor: Position gespeichert");
  Log("Unten ausrichten (18 px) für: Task-Manager, DU Meter, GPU-Z, Discord");
  Log("Task-Manager: Unten: 18 px");
  Log("GPU-Z: Unten: 18 px");
  Log("Discord: Unten: 18 px");
  Log("Zum dauerhaften Übernehmen der ausgerichteten Fenster jetzt Positionen speichern.");
 }
 // Screenshots: Seiten "Kanten je Programm" und "Programm hinzufügen" im Hauptfenster.
 internal void DemoShowEdges(){EditEdges();}
 internal void DemoShowAdd(){AddProgram();}
 internal void DemoShowOneTime() {
  AlignOneWindow();
  var page=view as OneTimeAlignForm;
  if(page!=null)page.DemoFill();
 }
 void UpdateSelection() {
  int count=list.Rows.Count(x=>x.Checked);
  selectionInfo.Text=count+" von "+list.Rows.Count+" ausgewählt";
  all.Text=list.Rows.Any(x=>!x.Checked)?"Alle auswählen":"Auswahl aufheben";
 }
 void SetBusy(bool on) {
  busy=on;list.Enabled=!on;
  foreach(var button in new Button[]{restore,save,all,alignTop,alignBottom,alignLeft,alignRight,alignHCenter,alignVCenter,alignAuto,alignRules,admin,addProgram,navEdges,navOneTime,arrangement})if(button!=null)button.Enabled=!on;
  activity.Active=on;
 }
 static string Quote(string s){return "\""+s+"\"";}
 // Programmliste aus Positionen.json; nicht installierte und selbst ausgeblendete Programme erscheinen nicht.
 // Screenshots (Demo): Startliste ohne gespeicherte Einstellungen.
 void LoadPrograms(out string skipped,out string hiddenNames) {
  skipped=null;hiddenNames=null;
  allPrograms.Clear();entries.Clear();
  if(DemoMode)allPrograms.AddRange(Demo.Programs());
  else if(profiles!=null&&profiles.ContainsKey(Store.ProgramsKey))allPrograms.AddRange(Store.Programs(profiles));
  // Übernahme fehlgeschlagen: wenigstens die bisherigen eigenen Programme anzeigen.
  else allPrograms.AddRange(Store.CustomEntries(profiles));
  entries.AddRange(allPrograms);
  if(DemoMode)return;
  var hidden=Store.HiddenIds(profiles);
  var missing=entries.Where(x=>!Installed(x)).ToList();
  var off=entries.Where(x=>hidden.Contains(x.Id)&&Installed(x)).ToList();
  if(missing.Count>0)skipped=String.Join(", ",missing.Select(x=>x.Name).ToArray());
  if(off.Count>0)hiddenNames=String.Join(", ",off.Select(x=>x.Name).ToArray());
  entries.RemoveAll(x=>missing.Contains(x)||off.Contains(x));
 }
 Entry ProgramById(string id){return allPrograms.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));}
 // Dienst neu starten (RestartService in der Programmliste, z.B. DUMeterSvc bei DU Meter).
 static void RestartService(string name) {
  using(var service=new System.ServiceProcess.ServiceController(name)) {
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
   Store.SaveProfiles(copy);
   profiles=copy;
  } catch(Exception ex) {Log("Abstand konnte nicht gespeichert werden: "+ex.Message);}
 }
 // Kanten je Programm (Fenster "Kanten je Programm"): angehakte Kanten werden bei "Automatisch" zusammen
 // ausgerichtet; die Schaltflächen Oben/Unten/Links/Rechts richten nur Programme aus, bei denen die Kante angehakt ist.
 // Oben + Unten = volle Höhe, Links + Rechts = volle Breite, Mitte = waagerecht mittig.
 // Gespeichert in Positionen.json ("_edges"); ohne Einstellung gilt die Vorgabe aus der Programmliste ("DefaultEdges").
 internal static readonly string[] AllEdges={"top","bottom","left","right","hcenter","vcenter"};
 // Vorgabe eines Programms: "DefaultEdges" in der Programmliste; ohne Angabe keine (frei wählbar).
 internal static HashSet<string> DefaultEdgesFor(Entry e) {
  return new HashSet<string>(e!=null&&e.DefaultEdges!=null?e.DefaultEdges:new string[0]);
 }
 HashSet<string> EdgesFor(string id) {
  var saved=profiles!=null&&profiles.ContainsKey("_edges")?profiles["_edges"] as Dictionary<string,object>:null;
  if(saved!=null&&saved.ContainsKey(id)) {
   var set=new HashSet<string>();
   var items=saved[id] as System.Collections.IEnumerable;
   if(items!=null&&!(saved[id] is string))foreach(object item in items)if(item!=null)set.Add(Convert.ToString(item));
   return set;
  }
  return DefaultEdgesFor(ProgramById(id));
 }
 // Eigene Abstände je Kante ("_edgegaps"): fehlt ein Wert, gilt der allgemeine Abstand.
 internal const int MaxEdgeGap=5000;
 Dictionary<string,int> GapsFor(string id) {
  var result=new Dictionary<string,int>();
  var saved=profiles!=null&&profiles.ContainsKey("_edgegaps")?profiles["_edgegaps"] as Dictionary<string,object>:null;
  if(saved==null||!saved.ContainsKey(id))return result;
  var values=saved[id] as Dictionary<string,object>;
  if(values==null)return result;
  foreach(var pair in values) {
   try {result[pair.Key]=Math.Max(0,Math.Min(MaxEdgeGap,Convert.ToInt32(pair.Value)));} catch {}
  }
  return result;
 }
 string WithGaps(string id,string edges) {
  var gaps=GapsFor(id);
  return String.Join(" ",(edges??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Select(x=>gaps.ContainsKey(x)?x+"="+gaps[x]:x).ToArray());
 }
 // Nachbarprogramm je Kante ("_edgerefs"): Kante richtet sich an der gegenüberliegenden Kante dieses Programms aus
 // (z.B. Links an Discord = linke Kante x px rechts neben Discord). Ohne Eintrag gilt der Bildschirmrand.
 Dictionary<string,string> RefsFor(string id) {
  var result=new Dictionary<string,string>();
  var saved=profiles!=null&&profiles.ContainsKey("_edgerefs")?profiles["_edgerefs"] as Dictionary<string,object>:null;
  if(saved==null||!saved.ContainsKey(id))return result;
  var values=saved[id] as Dictionary<string,object>;
  if(values==null)return result;
  foreach(var pair in values)if(pair.Value!=null&&Convert.ToString(pair.Value).Length>0)result[pair.Key]=Convert.ToString(pair.Value);
  return result;
 }
 Entry EntryById(string id){return entries.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));}
 // Kanten mit Nachbarprogramm um die Bezugslinie ergänzen (">x"); ist der Nachbar nicht offen, bleibt die Kante unverändert.
 string Resolve(string id,string edges,List<string> notes) {
  var refs=RefsFor(id);
  if(refs.Count==0)return edges;
  var result=new List<string>();
  foreach(string token in (edges??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)) {
   string name=token.Split('=')[0];
   string refId;
   Entry neighbor=refs.TryGetValue(name,out refId)?EntryById(refId):null;
   if(neighbor==null){result.Add(token);continue;}
   Rectangle r;
   if(!VisibleAlignment.TryVisibleRect(neighbor,out r)) {
    if(notes!=null)notes.Add(DirectionName(name)+" unverändert ("+neighbor.Name+" ist nicht offen)");
    continue;
   }
   int line=name=="left"?r.Right:name=="right"?r.Left:name=="top"?r.Bottom:r.Top;
   result.Add(token+">"+line);
   if(notes!=null)notes.Add(DirectionName(name)+" an "+neighbor.Name);
  }
  return String.Join(" ",result.ToArray());
 }
 // Reihenfolge: Programme, an denen sich andere ausrichten, zuerst (Kreisbezüge werden einfach übergangen).
 List<ProgramRow> OrderByNeighbors(List<ProgramRow> rows) {
  var result=new List<ProgramRow>();
  var done=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  Action<ProgramRow> visit=null;
  visit=row=>{
   string id=row.Entry.Id;
   if(done.Contains(id)||visiting.Contains(id))return;
   visiting.Add(id);
   foreach(string refId in RefsFor(id).Values) {
    var before=rows.FirstOrDefault(x=>String.Equals(x.Entry.Id,refId,StringComparison.OrdinalIgnoreCase));
    if(before!=null)visit(before);
   }
   visiting.Remove(id);done.Add(id);result.Add(row);
  };
  foreach(var row in rows)visit(row);
  return result;
 }
 bool Skipped(string mode,string id) {
  return mode!="auto"&&!EdgesFor(id).Contains(mode);
 }
 // Kanten für "Automatisch" in fester Reihenfolge; Mitte nur, wenn weder links noch rechts angehakt ist.
 string AutoEdges(string id) {
  var set=EdgesFor(id);
  if(set.Contains("left")||set.Contains("right"))set.Remove("hcenter");
  if(set.Contains("top")||set.Contains("bottom"))set.Remove("vcenter");
  return String.Join(" ",AllEdges.Where(x=>set.Contains(x)).ToArray());
 }
 // Einmalig ausrichten: ein angeklicktes Fenster ausrichten; nichts wird gespeichert.
 void AlignOneWindow() {
  if(busy)return;
  var page=new OneTimeAlignForm(Icon,()=>CurrentGap(),Log);
  ShowView(navOneTime,page,null,null);
  if(view==page&&!DemoMode)BeginInvoke((Action)page.Begin);
 }
 void EditEdges() {
  if(busy)return;
  var current=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  var currentGaps=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  var currentRefs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var e in entries){current[e.Id]=EdgesFor(e.Id);currentGaps[e.Id]=GapsFor(e.Id);currentRefs[e.Id]=RefsFor(e.Id);}
  var page=new AlignRulesForm(Icon,entries,current,currentGaps,currentRefs,CurrentGap());
  // Gespeichert wird erst mit "Speichern"; schlägt das fehl, bleibt die Seite mit der Fehlermeldung offen.
  // Danach bleibt die Seite offen und zeigt kurz grün "Gespeichert"; die Einzelheiten stehen im Protokoll.
  page.Save=()=>SaveEdges(page.Rules,page.Gaps,page.Refs);
  ShowView(navEdges,page,null,null);
 }
 string SaveEdges(Dictionary<string,HashSet<string>> result,Dictionary<string,Dictionary<string,int>> resultGaps,Dictionary<string,Dictionary<string,string>> resultRefs) {
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var saved=copy.ContainsKey("_edges")?copy["_edges"] as Dictionary<string,object>:null;
  if(saved==null){saved=new Dictionary<string,object>();copy["_edges"]=saved;}
  foreach(var pair in result)saved[pair.Key]=AllEdges.Where(x=>pair.Value.Contains(x)).ToList();
  var savedGaps=copy.ContainsKey("_edgegaps")?copy["_edgegaps"] as Dictionary<string,object>:null;
  if(savedGaps==null){savedGaps=new Dictionary<string,object>();copy["_edgegaps"]=savedGaps;}
  foreach(var pair in resultGaps) {
   if(pair.Value.Count==0)savedGaps.Remove(pair.Key);
   else savedGaps[pair.Key]=pair.Value.ToDictionary(x=>x.Key,x=>(object)x.Value);
  }
  var savedRefs=copy.ContainsKey("_edgerefs")?copy["_edgerefs"] as Dictionary<string,object>:null;
  if(savedRefs==null){savedRefs=new Dictionary<string,object>();copy["_edgerefs"]=savedRefs;}
  foreach(var pair in resultRefs) {
   if(pair.Value.Count==0)savedRefs.Remove(pair.Key);
   else savedRefs[pair.Key]=pair.Value.ToDictionary(x=>x.Key,x=>(object)x.Value);
  }
  copy.Remove("_skip");
  try {Store.SaveProfiles(copy);}
  catch(Exception ex) {return "Nicht gespeichert: "+ex.Message;}
  profiles=copy;
  var lines=new List<string>();
  foreach(var e in entries) {
   HashSet<string> set;
   Dictionary<string,int> gaps;
   if(!resultGaps.TryGetValue(e.Id,out gaps))gaps=new Dictionary<string,int>();
   Dictionary<string,string> refs;
   if(!resultRefs.TryGetValue(e.Id,out refs))refs=new Dictionary<string,string>();
   if(result.TryGetValue(e.Id,out set))lines.Add(e.Name+": "+(set.Count>0?String.Join(", ",AllEdges.Where(x=>set.Contains(x)).Select(x=>DirectionName(x)+(gaps.ContainsKey(x)?" "+gaps[x]+" px":"")+(refs.ContainsKey(x)&&EntryById(refs[x])!=null?" an "+EntryById(refs[x]).Name:"")).ToArray()):"keine"));
  }
  Log("Kanten je Programm gespeichert – "+String.Join("; ",lines.ToArray()));
  return null;
 }
 static string DirectionName(string mode) {
  switch(mode){case "top":return "Oben";case "bottom":return "Unten";case "left":return "Links";case "right":return "Rechts";case "hcenter":return "↔ Mitte";case "vcenter":return "↕ Mitte";default:return "Automatisch";}
 }
 async Task AlignWindows(string mode) {
  // Nachbarprogramme zuerst bearbeiten, damit sich andere an ihrer neuen Lage ausrichten.
  var selected=OrderByNeighbors(list.Rows.Where(x=>x.Checked).ToList());
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
    if(EdgesFor(entry.Id).Count==0){item.Status="Keine Kanten festgelegt";continue;}
    if(Skipped(mode,entry.Id)){item.Status="Ausrichten übersprungen";continue;}
    if(mode=="auto") {
     // Alle angehakten Kanten aus "Kanten je Programm" auf einmal.
     targetEdges=AutoEdges(entry.Id);
     if(targetEdges.Length==0){item.Status="Ausrichten übersprungen";continue;}
    }
    item.Status="Fensterkante wird gemessen …";
    try {
     string edges=targetEdges;
     // Eigene Abstände je Kante (Kanten je Programm) als "left=1200" anhängen.
     var notes=new List<string>();
     string withGaps=Resolve(entry.Id,WithGaps(entry.Id,edges),notes);
     string status=await Task.Run(()=>VisibleAlignment.AlignEntry(entry,gap,withGaps));
     if(notes.Count>0)status+="; "+String.Join("; ",notes.ToArray());
     if(entry.CenterOthers) {
      // Z.B. bei Easy2Sync weitere offene Fenster (z.B. Synchronisation) waagerecht mittig über dem Hauptfenster setzen;
      // ihre Größe und senkrechte Lage bleiben.
      var extra=await Task.Run(()=>VisibleAlignment.CenterOthersOnMain(entry));
      foreach(string line in extra)Log(entry.Name+" – "+line);
      if(extra.Count>0)status+="; "+extra.Count+" weitere(s) Fenster";
     }
     // Ab 2.33 wird keine eigene Regel mehr gespeichert: Wiederherstellen und die Nachkorrektur beim Start
     // verwenden immer die Tabelle "Kanten je Programm".
     if(entry.RestartService!=null) {
      Log(entry.Name+" ist ausgerichtet. Dienst "+entry.RestartService+" wird neu gestartet …");
      try {await Task.Run(()=>RestartService(entry.RestartService));status+="; Dienst neu gestartet";}
      catch(Exception serviceError){throw new Exception("Fenster ausgerichtet; Dienst-Neustart fehlgeschlagen: "+serviceError.Message);}
     }
     item.Status=status;
     Log(entry.Name+": "+status);
    }catch(Exception ex){item.Status=ex.Message.StartsWith("Fenster ausgerichtet;")?"Ausgerichtet; Dienstfehler – siehe Protokoll":"Nicht ausgerichtet – siehe Protokoll";Log(entry.Name+": "+ex.Message);}
   }
   Log("Zum dauerhaften Übernehmen der ausgerichteten Fenster jetzt Positionen speichern.");
  } finally {SetBusy(false);}
 }
 // Zeilen der Startseite: 0 Update-Hinweis, 1 Aktivität, 2 Programme, 3 Aktionen, 4 Ausrichten, 5 Hinweis, 6 Protokoll
 // (Kopf und Fußzeile liegen außerhalb, über die volle Fensterbreite).
 const int UpdateRow=0,ProgramsRow=2;
 // Höhe des Programmbereichs: bis zu 12 Zeilen sichtbar, darüber wird die Liste gescrollt.
 int ProgramsHeight(int count){return list.RowHeight*Math.Max(1,Math.Min(count,16))+Ui.Px(44)+Ui.Px(16)+Ui.Px(14)+2;}
 // Nach Hinzufügen/Entfernen: Programmbereich anpassen, das Fenster wächst bzw. schrumpft mit (begrenzt auf den Bildschirm).
 void UpdateProgramsHeight() {
  if(layout==null)return;
  int target=ProgramsHeight(list.LineCount);
  int delta=target-(int)layout.RowStyles[ProgramsRow].Height;
  if(delta==0)return;
  layout.RowStyles[ProgramsRow].Height=target;
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
  var page=new AddProgramForm(Icon,allPrograms);
  // Unterseite von "Programme": "Hinzufügen" übernimmt, "Abbrechen" bzw. ein Wechsel über die Navigation verwirft.
  ShowView(navPrograms,page,result=>{if(result==DialogResult.OK&&page.Result!=null)AddEntry(page.Result);},"Programm hinzufügen");
 }
 void AddEntry(Entry entry) {
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  string status="Hinzugefügt – Position noch nicht gespeichert";
  string note=null;
  try {
   // Aktuelle Fensterposition gleich mitspeichern.
   copy[entry.Id]=new Dictionary<string,object>{{"Window",VisibleAlignment.CaptureWindow(entry)}};
   status="Position gespeichert";
  } catch(Exception ex) {note=ex.Message;}
  var updated=new List<Entry>(allPrograms);updated.Add(entry);
  Store.SetPrograms(copy,updated);
  try {Store.SaveProfiles(copy);}
  catch(Exception ex) {Log(entry.Name+": konnte nicht gespeichert werden – "+ex.Message);return;}
  profiles=copy;
  allPrograms.Add(entry);
  var added=new ProgramRow{Entry=entry,Name=entry.Name,Checked=false};
  list.Add(added);
  SyncEntries();
  UpdateProgramsHeight();
  UpdateSelection();
  added.Status=status;
  Log("Programm hinzugefügt: "+entry.Name+" ("+entry.ProcessName+".exe"+(entry.WindowTitle!=null?", Fenster „"+entry.WindowTitle+"“":"")+(String.IsNullOrEmpty(entry.Exe)?", Programmpfad unbekannt – wird nicht automatisch gestartet":"")+")");
  if(note!=null)Log(entry.Name+": "+note+" Danach \"Positionen speichern\" verwenden.");
  else Log(entry.Name+": "+status);
 }
 // Programm gilt als installiert, wenn die Programmdatei existiert oder es gerade läuft (ohne Programmpfad immer).
 static bool Installed(Entry e) {
  if(String.IsNullOrEmpty(e.Exe))return true;
  try {
   // Ab 2.68 erst die Pfadprüfung (Netzwerk/Reparse Point ohne Freigabe: kein Zugriff, Eintrag bleibt sichtbar,
   // der Start meldet dann den Grund), dann nachsehen, ob die Datei vorhanden ist.
   if(Store.ProgramPathProblem(e)!=null)return true;
   if(Store.ProgramFileExists(e))return true;
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
 List<Entry> HiddenPrograms() {
  var hidden=Store.HiddenIds(profiles);
  return allPrograms.Where(x=>hidden.Contains(x.Id)&&!entries.Contains(x)&&Installed(x)).ToList();
 }
 void ShowRowMenu(object sender,RowMenuEventArgs e) {
  if(busy)return;
  var row=e.Row;
  var hidden=HiddenPrograms();
  var groups=list.Groups();
  // Menü bei jedem Öffnen neu aufbauen, damit Farben und Symbol zum aktuellen Hell-/Dunkelmodus passen.
  MenuStyle.Prepare(rowMenu);
  rowMenu.Items.Add(MenuStyle.TopSpacer());
  if(row!=null&&row.Entry!=null) {
   rowMenu.Items.Add(MenuStyle.Item("„"+row.Name+"“ umbenennen …",'\uE8AC',(s,a)=>BeginInvoke((Action)(()=>RenameProgram(row)))));
   rowMenu.Items.Add(MenuStyle.Item("Gruppe …",'\uE8FD',(s,a)=>BeginInvoke((Action)(()=>ShowGroupChoice(row,e.Location)))));
   rowMenu.Items.Add(MenuStyle.Item("„"+row.Name+"“ ausblenden",'\uED1A',(s,a)=>HideProgram(row)));
   rowMenu.Items.Add(MenuStyle.Item("„"+row.Name+"“ entfernen …",'\uE74D',(s,a)=>RemoveProgram(row)));
  }
  if(e.Group!=null) {
   string group=e.Group;
   rowMenu.Items.Add(MenuStyle.Item("Gruppe „"+group+"“ umbenennen …",'\uE8AC',(s,a)=>BeginInvoke((Action)(()=>RenameGroup(group)))));
   rowMenu.Items.Add(MenuStyle.Item("Gruppe „"+group+"“ auflösen",'\uE711',(s,a)=>DissolveGroup(group)));
  }
  if(groups.Count>1) {
   rowMenu.Items.Add(new ToolStripSeparator());
   rowMenu.Items.Add(MenuStyle.Item("Alle Gruppen einklappen",'\uE70E',(s,a)=>SetAllCollapsed(true)));
   rowMenu.Items.Add(MenuStyle.Item("Alle Gruppen ausklappen",'\uE70D',(s,a)=>SetAllCollapsed(false)));
  }
  if(hidden.Count>0) {
   rowMenu.Items.Add(new ToolStripSeparator());
   rowMenu.Items.Add(MenuStyle.Item("Ausgeblendete wieder anzeigen ("+hidden.Count+")",'\uE890',(s,a)=>ShowHiddenPrograms()));
  }
  if(rowMenu.Items.Count>1)rowMenu.Items.Add(new ToolStripSeparator());
  // Sortieren: nur innerhalb der Gruppe der Zeile bzw. innerhalb jeder Gruppe; die Reihenfolge der Gruppen bleibt.
  string block=e.Group??(row!=null&&row.Entry!=null?row.Entry.Group??"":null);
  if(block!=null&&list.Rows.Count(x=>String.Equals(x.Entry.Group??"",block,StringComparison.CurrentCultureIgnoreCase))>1)
   rowMenu.Items.Add(MenuStyle.Item(block.Length==0?"Programme ohne Gruppe nach Name sortieren":"Gruppe „"+block+"“ nach Name sortieren",'\uE8CB',(s,a)=>SortByName(block)));
  if(list.Rows.Count>1)rowMenu.Items.Add(MenuStyle.Item(groups.Count>0?"Alle Gruppen einzeln nach Name sortieren":"Nach Name sortieren",'\uE8CB',(s,a)=>SortByName(null)));
  rowMenu.Items.Add(MenuStyle.Item("Kanten je Programm …",'\uE9A6',(s,a)=>EditEdges()));
  rowMenu.Items.Add(MenuStyle.BottomSpacer());
  MenuStyle.ShowOnScreen(rowMenu,list,e.Location,ToolStripDropDownDirection.BelowRight);
 }
 // ---------------------------------------------------------------- Gruppen (ab 2.52)
 // Gruppe je Programm in der Programmliste ("Group"), eingeklappte Gruppen unter "_gruppenzu" (für alle Anordnungen).
 readonly ContextMenuStrip groupMenu=new ContextMenuStrip();
 List<string> CollapsedGroups() {
  var result=new List<string>();
  var items=profiles!=null&&profiles.ContainsKey(Store.CollapsedKey)?profiles[Store.CollapsedKey] as System.Collections.IEnumerable:null;
  if(items!=null&&!(items is string))foreach(object item in items)if(item!=null)result.Add(Convert.ToString(item));
  return result;
 }
 void SaveCollapsed() {
  if(DemoMode)return;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var names=list.Collapsed.Where(x=>allPrograms.Any(p=>String.Equals(p.Group,x,StringComparison.CurrentCultureIgnoreCase))).OrderBy(x=>x).ToList<object>();
  if(names.Count==0)copy.Remove(Store.CollapsedKey);else copy[Store.CollapsedKey]=names;
  try {Store.SaveProfiles(copy);profiles=copy;}
  catch(Exception ex) {Log("Gruppen konnten nicht gespeichert werden – "+ex.Message);}
 }
 // entries in Anzeigereihenfolge der Liste halten (Gruppen ordnen die Zeilen um).
 void SyncEntries(){entries.Clear();entries.AddRange(list.Rows.Select(x=>x.Entry));}
 // Gruppen der Programme geändert (Entry.Group schon gesetzt): Liste neu ordnen und speichern.
 void SaveGroups(string message) {
  list.Rebuild();
  UpdateProgramsHeight();
  UpdateSelection();
  SaveOrder(list.Rows.Select(x=>x.Entry).ToList(),message);
  SaveCollapsed();
 }
 // Anzeigename ändern (ab 2.52), z.B. "Epic Games Launcher" → "Epic". Id, Prozess und gespeicherte Werte bleiben.
 void RenameProgram(ProgramRow row) {
  if(busy)return;
  var entry=row.Entry;
  string old=entry.Name;
  string name=InputDialog.Ask(this,"Programm umbenennen","Nur der Name in der Liste ändert sich; Positionen, Kanten und Gruppe bleiben.",old,"Umbenennen",text=>{
   if(text.Length==0)return "Bitte einen Namen eingeben.";
   if(text.Length>60)return "Höchstens 60 Zeichen.";
   if(allPrograms.Any(x=>x!=entry&&String.Equals(x.Name,text,StringComparison.CurrentCultureIgnoreCase)))return "Diesen Namen hat schon ein anderes Programm.";
   return null;
  });
  if(name==null)return;
  name=name.Trim();
  if(name==old)return;
  entry.Name=name;row.Name=name;
  list.Invalidate();
  SaveOrder(list.Rows.Select(x=>x.Entry).ToList(),"„"+old+"“ heißt jetzt „"+name+"“.");
 }
 void ShowGroupChoice(ProgramRow row,Point location) {
  if(busy)return;
  string current=row.Entry.Group??"";
  MenuStyle.Prepare(groupMenu);
  groupMenu.Items.Add(MenuStyle.TopSpacer());
  groupMenu.Items.Add(MenuStyle.Choice("Ohne Gruppe",current.Length==0,(s,a)=>SetGroup(row,null)));
  foreach(string group in list.Groups()) {
   string name=group;
   groupMenu.Items.Add(MenuStyle.Choice(name,String.Equals(current,name,StringComparison.CurrentCultureIgnoreCase),(s,a)=>SetGroup(row,name)));
  }
  groupMenu.Items.Add(new ToolStripSeparator());
  groupMenu.Items.Add(MenuStyle.Choice("Neue Gruppe …",false,(s,a)=>BeginInvoke((Action)(()=>NewGroup(row)))));
  groupMenu.Items.Add(MenuStyle.BottomSpacer());
  groupMenu.Opened-=GroupMenuOpened;groupMenu.Opened+=GroupMenuOpened;
  MenuStyle.ShowOnScreen(groupMenu,list,location,ToolStripDropDownDirection.BelowRight);
 }
 void GroupMenuOpened(object sender,EventArgs e){MenuStyle.RoundCorners(groupMenu);}
 void SetGroup(ProgramRow row,string group) {
  if(busy)return;
  string old=row.Entry.Group??"";
  if(String.Equals(old,group??"",StringComparison.CurrentCultureIgnoreCase))return;
  // Unter vorhandener Schreibweise einsortieren.
  if(group!=null)group=list.Groups().FirstOrDefault(x=>String.Equals(x,group,StringComparison.CurrentCultureIgnoreCase))??group;
  row.Entry.Group=group;
  // Neue Mitglieder ans Ende ihrer Gruppe.
  list.Rows.Remove(row);list.Rows.Add(row);
  if(group!=null)list.Collapsed.Remove(group);
  SaveGroups(row.Name+(group==null?": ohne Gruppe.":" → Gruppe „"+group+"“."));
 }
 Func<string,string> GroupNameCheck(string old) {
  return name=>{
   if(name.Length==0)return "Bitte einen Namen eingeben.";
   if(name.Length>40)return "Höchstens 40 Zeichen.";
   if(old==null||!String.Equals(old,name,StringComparison.CurrentCultureIgnoreCase))
    if(allPrograms.Any(x=>String.Equals(x.Group,name,StringComparison.CurrentCultureIgnoreCase)))return "Diese Gruppe gibt es schon.";
   return null;
  };
 }
 void NewGroup(ProgramRow row) {
  if(busy)return;
  string name=InputDialog.Ask(this,"Neue Gruppe","„"+row.Name+"“ kommt in die neue Gruppe. Weitere Programme per Rechtsklick › „Gruppe …“ oder durch Ziehen hinzufügen.","","Anlegen",GroupNameCheck(null));
  if(name==null)return;
  SetGroup(row,name.Trim());
 }
 void RenameGroup(string old) {
  if(busy)return;
  string name=InputDialog.Ask(this,"Gruppe umbenennen",null,old,"Umbenennen",GroupNameCheck(old));
  if(name==null)return;
  name=name.Trim();
  if(name==old)return;
  foreach(var e in allPrograms)if(String.Equals(e.Group,old,StringComparison.CurrentCultureIgnoreCase))e.Group=name;
  if(list.Collapsed.Remove(old))list.Collapsed.Add(name);
  SaveGroups("Gruppe „"+old+"“ heißt jetzt „"+name+"“.");
 }
 void DissolveGroup(string group) {
  if(busy)return;
  foreach(var e in allPrograms)if(String.Equals(e.Group,group,StringComparison.CurrentCultureIgnoreCase))e.Group=null;
  list.Collapsed.Remove(group);
  SaveGroups("Gruppe „"+group+"“ aufgelöst; ihre Programme stehen jetzt ohne Gruppe.");
 }
 void SetAllCollapsed(bool collapsed) {
  if(busy)return;
  list.Collapsed.Clear();
  if(collapsed)foreach(string group in list.Groups())list.Collapsed.Add(group);
  list.Rebuild();
  SaveCollapsed();
  UpdateProgramsHeight();
 }
 // Programm ausblenden; es bleibt in der Programmliste, gespeicherte Werte bleiben erhalten.
 void HideProgram(ProgramRow row) {
  if(busy)return;
  var entry=row.Entry;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var ids=Store.HiddenIds(copy);ids.Add(entry.Id);
  Store.SetHiddenIds(copy,ids);
  try {Store.SaveProfiles(copy);}
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
  var back=HiddenPrograms();
  if(back.Count==0)return;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  var ids=Store.HiddenIds(copy);
  foreach(var e in back)ids.Remove(e.Id);
  Store.SetHiddenIds(copy,ids);
  try {Store.SaveProfiles(copy);}
  catch(Exception ex) {Log("Ausgeblendete Programme konnten nicht angezeigt werden – "+ex.Message);return;}
  profiles=copy;
  foreach(var e in back) {
   // An der ursprünglichen Stelle einfügen (Reihenfolge der Programmliste).
   int pos=entries.Count(x=>allPrograms.IndexOf(x)<allPrograms.IndexOf(e));
   entries.Insert(pos,e);
   list.Insert(pos,new ProgramRow{Entry=e,Name=e.Name,Checked=false});
  }
  SyncEntries();
  UpdateProgramsHeight();
  UpdateSelection();
  Log("Wieder angezeigt: "+String.Join(", ",back.Select(x=>x.Name).ToArray()));
 }
 // Reihenfolge (ab 2.52): Ziehen in der Liste, Strg+Pfeil oder "Nach Name sortieren"; gespeichert als
 // Reihenfolge von "_programme". Ausgeblendete und nicht installierte Programme behalten ihre Plätze,
 // die angezeigten werden in der neuen Reihenfolge auf ihre bisherigen Plätze verteilt.
 void SaveOrder(List<Entry> shown,string message) {
  var slots=new Queue<Entry>(shown);
  var updated=allPrograms.Select(x=>entries.Contains(x)&&slots.Count>0?slots.Dequeue():x).ToList();
  if(!DemoMode) {
   var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
   Store.SetPrograms(copy,updated);
   try {Store.SaveProfiles(copy);}
   catch(Exception ex) {Log("Reihenfolge konnte nicht gespeichert werden – "+ex.Message);return;}
   profiles=copy;
  }
  allPrograms.Clear();allPrograms.AddRange(updated);
  entries.Clear();entries.AddRange(shown);
  if(message!=null)Log(message);
 }
 // group: nur diese Gruppe ("" = Programme ohne Gruppe) bzw. null = jede Gruppe für sich. Die Reihenfolge der
 // Gruppen bleibt, nur die Programme innerhalb werden nach Name geordnet.
 void SortByName(string group) {
  if(busy)return;
  Func<Entry,string> blockOf=x=>x.Group??"";
  var blocks=entries.Select(blockOf).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
  var sorted=new List<Entry>();
  foreach(string block in blocks) {
   var members=entries.Where(x=>String.Equals(blockOf(x),block,StringComparison.CurrentCultureIgnoreCase)).ToList();
   if(group==null||String.Equals(group,block,StringComparison.CurrentCultureIgnoreCase))
    members=members.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
   sorted.AddRange(members);
  }
  if(sorted.SequenceEqual(entries))return;
  // Zeilen samt Haken und Status mitnehmen.
  var rows=list.Rows.OrderBy(x=>sorted.IndexOf(x.Entry)).ToList();
  list.Rows.Clear();list.Rows.AddRange(rows);
  list.Rebuild();
  UpdateSelection();
  SaveOrder(sorted,group==null?"Programme innerhalb jeder Gruppe nach Name sortiert.":group.Length==0?"Programme ohne Gruppe nach Name sortiert.":"Gruppe „"+group+"“ nach Name sortiert.");
 }
 // Einstellungen exportieren/importieren (ab 2.52): die ganze Positionen.json mit Programmliste, Positionen,
 // Kanten und Anordnungen. Der Import ersetzt alles (vorher Sicherung) und startet das Programm neu.
 void ExportSettings() {
  using(var dialog=new SaveFileDialog{Title="Einstellungen exportieren",Filter="Einstellungen (*.json)|*.json",FileName="Fensterpositionen-Einstellungen.json",OverwritePrompt=true}) {
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   try {
    Store.ExportSettings(dialog.FileName);
    Log("Einstellungen exportiert ("+allPrograms.Count+" Programme, Anordnungen und Positionen): "+Store.Display(dialog.FileName));
   } catch(Exception ex) {MessageBox.Show(this,"Die Einstellungen konnten nicht exportiert werden.\r\n\r\n"+ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
 }
 void ImportSettings() {
  if(busy)return;
  Dictionary<string,object> data;
  string file;
  using(var dialog=new OpenFileDialog{Title="Einstellungen importieren",Filter="Einstellungen (*.json)|*.json"}) {
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   file=dialog.FileName;
   try {data=Store.ReadSettings(file);}
   catch(Exception ex) {MessageBox.Show(this,"Die Datei kann nicht importiert werden.\r\n\r\n"+ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
  }
  // Vorschau (ab 2.67): Netzwerkpfade und Sonderfälle nur mit eigenem Schalter, Vorauswahl „Abbrechen“.
  // Ab 2.68: Startbefehle und Schreibziele nur „wie auf diesem PC“ oder je Eintrag freigegeben.
  bool network,special;
  List<string> approved;
  using(var preview=new ImportPreviewForm(Icon,file,Store.AnalyzeImport(data),Store.Arrangements(data).Count+1)) {
   if(preview.ShowDialog(this)!=DialogResult.OK)return;
   network=preview.Network;special=preview.Special;approved=preview.ApprovedIds;
  }
  if(!ConfirmLeaveView())return;
  CloseView();
  string backup;
  try {data=Store.PrepareImport(data,network,special,approved);backup=Store.ReplaceSettings(data);}
  catch(Exception ex) {Log("Einstellungen konnten nicht importiert werden – "+ex.Message);MessageBox.Show(this,"Die Einstellungen konnten nicht importiert werden.\r\n\r\n"+ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
  Log("Einstellungen importiert aus "+Store.Display(file)+(backup!=null?" – Sicherung der bisherigen: "+Store.Display(backup):"")+". Programm wird neu gestartet …");
  try {Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});}
  catch(Exception ex) {MessageBox.Show(this,"Die Einstellungen sind importiert, aber "+AppInfo.Product+" konnte nicht neu gestartet werden. Bitte von Hand starten.\r\n\r\n"+ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  Close();
 }
 void RemoveProgram(ProgramRow row) {
  if(busy)return;
  var entry=row.Entry;
  if(MessageBox.Show(this,"„"+entry.Name+"“ wird aus der Programmliste in Positionen.json gelöscht, ebenso die gespeicherte Position und die Kanten-Einstellungen dieser Anordnung. Das Programm selbst bleibt unverändert.\r\n\r\nNur vorübergehend aus der Liste nehmen: „Ausblenden“ verwenden.\r\n\r\n„"+entry.Name+"“ entfernen?","Programm entfernen",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
  var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
  copy.Remove(entry.Id);
  // Auch Ausrichtungsregel, Kanten und eigene Abstände des Programms entfernen.
  foreach(string key in new[]{"_alignment","_edges","_edgegaps","_edgerefs"}) {
   var rules=copy.ContainsKey(key)?copy[key] as Dictionary<string,object>:null;
   if(rules!=null)rules.Remove(entry.Id);
  }
  Store.SetPrograms(copy,allPrograms.Where(x=>x!=entry));
  try {Store.SaveProfiles(copy);}
  catch(Exception ex) {Log(entry.Name+": konnte nicht entfernt werden – "+ex.Message);return;}
  profiles=copy;
  allPrograms.Remove(entry);
  entries.Remove(entry);
  list.Remove(row);
  UpdateProgramsHeight();
  UpdateSelection();
  Log("Programm entfernt: "+entry.Name);
 }
 // Protokoll im Fenster und zusätzlich als Tagesdatei in TraceLogs.
 // Eine Zeile je Eintrag: Zeilenumbrüche und Steuerzeichen (z.B. aus Fenstertiteln) werden maskiert (ab 2.68).
 void Log(string text){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+TraceLog.OneLine(text)+Environment.NewLine);TraceLog.Write(text);}
 bool Stop(Entry e) {
  // Geplante Aufgabe, die das Programm wieder startet, vorher beenden (EndTask, z.B. beim Task-Manager).
  // Ab 2.68 über die Aufgabenplanung (Schedule.Service) mit Prüfung: Scheitert das Beenden oder läuft die Aufgabe
  // nach 5 Sekunden noch, bricht der Vorgang für dieses Programm ab, bevor etwas geändert wird.
  if(e.EndTask!=null)EndScheduledTask(e.EndTask);
  // Nur Prozesse dieses Programms (OwnProcesses); gleichnamige fremde Prozesse bleiben unberührt.
  var skipped=new HashSet<string>();
  var first=OwnProcesses(e,skipped);
  // Läuft das Programm aus diesem Ordner in dieser Sitzung, aber nicht prüfbar als eigener Prozess (z.B. mit
  // Adminrechten oder über "Als anderer Benutzer ausführen"), wird nichts geändert: sonst würde in die Einstellungen eines laufenden Programms
  // geschrieben und ein zweites gestartet.
  foreach(string id in skipped) {
   int pid;
   if(Int32.TryParse(id,out pid)&&InExeFolder(pid,e)) {
    foreach(var p in first)p.Dispose();
    throw new Exception("Läuft mit anderen Rechten oder unter einem anderen Konto (PID: "+id+") – wird nicht beendet. Bitte selbst beenden und erneut versuchen.");
   }
  }
  if(skipped.Count>0) {
   string pids=String.Join(", ",skipped.ToArray());
   BeginInvoke((Action)(()=>Log(e.Name+": gleichnamige Prozesse aus einem anderen Ordner, einer anderen Sitzung oder einem anderen Benutzerkonto bleiben unberührt (PID: "+pids+").")));
  }
  bool running=false;
  foreach(var p in first) using(p) {
   try { if(!p.HasExited){running=true;p.CloseMainWindow();} }
   catch(InvalidOperationException){}
  }
  if(!running)return false;
  // Electron applications may hide in the tray and spawn helper processes.
  // Allow a normal exit first (WM_CLOSE über CloseMainWindow, 5 Sekunden), then stop remaining processes of this program.
  // Erzwungen beendet wird nur mit "ForceKill" oder nach Rückfrage, und dann nur genau die Prozesse, die beim
  // Durchlauf über ein offenes Handle gebunden wurden (PID plus Startzeit, eigener Pfad, eigene Sitzung, eigenes
  // Benutzerkonto); beendet wird über dieses Handle (TerminateProcess). Später gestartete bleiben unberührt.
  var timer=Stopwatch.StartNew();
  var targets=new List<BoundProcess>();
  bool force=false;
  try {
   while(timer.ElapsedMilliseconds<20000) {
    var processes=OwnProcesses(e,null);
    if(processes.Count==0) {
     Thread.Sleep(500);
     var check=OwnProcesses(e,null);
     bool empty=check.Count==0;
     foreach(var p in check)p.Dispose();
     if(empty)return true;
     continue;
    }
    if(!force&&timer.ElapsedMilliseconds>=5000) {
     foreach(var p in processes) {
      BoundProcess bound=null;
      try {bound=BindProcess(p.Id,e);} catch {}
      if(bound!=null)targets.Add(bound);
     }
     if(targets.Count==0) {
      foreach(var p in processes)p.Dispose();
      throw new Exception("Nicht beendet – reagiert nicht auf das normale Schließen und lässt sich nicht sicher erzwungen beenden. Bitte selbst beenden und erneut versuchen.");
     }
     if(!e.ForceKill) {
      string answer=AskForceKill(e,targets.Select(x=>x.Pid).ToList());
      if(answer==null) {
       foreach(var p in processes)p.Dispose();
       throw new Exception("Nicht beendet – reagiert nicht auf das normale Schließen. Bitte selbst beenden und erneut versuchen.");
      }
     }
     force=true;
     timer=Stopwatch.StartNew();
    }
    foreach(var p in processes) using(p) {
     try {if(!force)p.CloseMainWindow();}
     catch(InvalidOperationException){} // Process exited between enumeration and access.
    }
    if(force)
     foreach(var t in targets)
      if(!t.Done) {TerminateProcess(t.Handle,1);t.Done=WaitForSingleObject(t.Handle,1500)==0;}
    Thread.Sleep(250);
   }
  } finally {foreach(var t in targets)CloseHandle(t.Handle);}
  var remaining=OwnProcesses(e,null);
  string ids=String.Join(", ",remaining.Select(p=>p.Id.ToString()).ToArray());
  foreach(var p in remaining)p.Dispose();
  if(remaining.Count==0)return true;
  throw new Exception("Programm ist nach 20 Sekunden noch aktiv (PID: "+ids+"). Bitte vollstaendig beenden und erneut versuchen.");
 }
 // Über ein offenes Handle gebundener Prozess (PID plus Startzeit): Solange das Handle offen ist, kann Windows die
 // Prozess-ID nicht neu vergeben; beendet wird nur über dieses Handle.
 sealed class BoundProcess {
  public int Pid;
  public long Start;
  public IntPtr Handle;
  public bool Done;
 }
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint exitCode);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ProcessIdToSessionId(int pid,out int session);
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,int access,out IntPtr token);
 // Handle öffnen (TERMINATE | SYNCHRONIZE | QUERY_LIMITED_INFORMATION) und über genau dieses Handle prüfen, dass es
 // ein Prozess dieses Programms ist (IsOwnProcess); sonst null.
 static BoundProcess BindProcess(int pid,Entry e) {
  IntPtr h=OpenProcess(0x0001|0x100000|0x1000,false,pid);
  if(h==IntPtr.Zero)return null;
  long creation,exit,kernel,user;
  if(!GetProcessTimes(h,out creation,out exit,out kernel,out user)||creation==0||!IsOwnProcess(h,pid,e)) {CloseHandle(h);return null;}
  return new BoundProcess{Pid=pid,Start=creation,Handle=h};
 }
 static readonly string OwnSid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
 static int OwnSession() {using(var self=Process.GetCurrentProcess())return self.SessionId;}
 // Prozess dieses Programms (ab 2.71): Programmdatei im Ordner der eingetragenen EXE oder darunter (Electron-Helfer
 // liegen in Unterordnern, z.B. Discord\app-1.0\Discord.exe neben Update.exe), eigene Windows-Sitzung und eigenes
 // Benutzerkonto. Alles über das übergebene Handle geprüft; was sich nicht lesen lässt, gilt nicht als eigener Prozess.
 static bool IsOwnProcess(IntPtr h,int pid,Entry e) {
  if(!ImageInExeFolder(h,e))return false;
  int session;
  if(!ProcessIdToSessionId(pid,out session)||session!=OwnSession())return false;
  IntPtr token;
  if(!OpenProcessToken(h,0x0008,out token))return false; // TOKEN_QUERY
  try {
   using(var identity=new System.Security.Principal.WindowsIdentity(token))
    return identity.User!=null&&identity.User.Value==OwnSid;
  } catch {return false;}
  finally {CloseHandle(token);}
 }
 // Programmdatei des Prozesses (Handle) liegt im Ordner der eingetragenen EXE oder darunter.
 static bool ImageInExeFolder(IntPtr h,Entry e) {
  string folder;
  try {
   if(String.IsNullOrEmpty(e.Exe))return false;
   folder=Path.GetDirectoryName(Path.GetFullPath(e.Exe)).TrimEnd('\\')+"\\";
  } catch {return false;}
  var name=new StringBuilder(1024);
  int size=name.Capacity;
  if(!QueryFullProcessImageName(h,0,name,ref size))return false;
  try {return Path.GetFullPath(name.ToString(0,size)).StartsWith(folder,StringComparison.OrdinalIgnoreCase);} catch {return false;}
 }
 static bool InExeFolder(int pid,Entry e) {
  IntPtr h=OpenProcess(0x1000,false,pid); // PROCESS_QUERY_LIMITED_INFORMATION
  if(h==IntPtr.Zero)return false;
  try {
   int session;
   return ImageInExeFolder(h,e)&&ProcessIdToSessionId(pid,out session)&&session==OwnSession();
  } finally {CloseHandle(h);}
 }
 // Geplante Aufgabe beenden (EndTask): Aufgabe über die Aufgabenplanung suchen; läuft sie (State 4 = Running),
 // Stop aufrufen und höchstens 5 Sekunden auf das Ende warten. Fehlt die Aufgabe oder scheitert etwas: Ausnahme mit Grund.
 static void EndScheduledTask(string name) {
  Type type=Type.GetTypeFromProgID("Schedule.Service");
  if(type==null)throw new Exception("Die Aufgabenplanung ist nicht verfügbar – geplante Aufgabe „"+name+"“ nicht beendet.");
  object service=null;
  try {
   service=Activator.CreateInstance(type);
   ComCall(service,"Connect",Missing.Value,Missing.Value,Missing.Value,Missing.Value);
   object folder=ComCall(service,"GetFolder","\\");
   object task;
   try {task=ComCall(folder,"GetTask",name.StartsWith("\\")?name:"\\"+name);}
   catch(Exception ex) {throw new Exception("Geplante Aufgabe „"+name+"“ nicht gefunden – nichts geändert. "+ComMessage(ex));}
   if(TaskState(task)!=4)return;
   ComCall(task,"Stop",0);
   var timer=Stopwatch.StartNew();
   while(TaskState(task)==4) {
    if(timer.ElapsedMilliseconds>5000)throw new Exception("Geplante Aufgabe „"+name+"“ läuft nach 5 Sekunden noch – nichts geändert.");
    Thread.Sleep(200);
   }
  } catch(Exception ex) {
   if(ex.Message.StartsWith("Geplante Aufgabe")||ex.Message.StartsWith("Die Aufgabenplanung"))throw;
   throw new Exception("Geplante Aufgabe „"+name+"“ konnte nicht beendet werden – nichts geändert. "+ComMessage(ex));
  } finally {
   if(service!=null)try {Marshal.ReleaseComObject(service);} catch {}
  }
 }
 static object ComCall(object target,string method,params object[] args) {
  return target.GetType().InvokeMember(method,BindingFlags.InvokeMethod,null,target,args);
 }
 static int TaskState(object task) {
  return Convert.ToInt32(task.GetType().InvokeMember("State",BindingFlags.GetProperty,null,task,null));
 }
 static string ComMessage(Exception ex) {
  var inner=ex is TargetInvocationException&&ex.InnerException!=null?ex.InnerException:ex;
  return inner.Message;
 }
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
 // Rückfrage vor dem erzwungenen Beenden (ab 2.68): "einmal", "immer" (künftig ohne Rückfrage, "ForceKill") oder
 // null (nicht beenden, vorausgewählt). Läuft im Hintergrund-Thread, die Rückfrage selbst im Fenster-Thread.
 string AskForceKill(Entry e,List<int> pids) {
  var ids=pids.Select(x=>x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
  string answer=null;
  Invoke((Action)(()=>{
   using(var dialog=new ForceKillForm(Icon,e.Name,e.ProcessName,ids)) {
    dialog.ShowDialog(this);
    answer=dialog.Answer;
   }
   if(answer=="immer")SetForceKill(e);
   Log(e.Name+": reagiert nicht auf das normale Schließen – "+(answer==null?"nicht beendet.":answer=="immer"?"wird beendet, künftig ohne Rückfrage (\"ForceKill\").":"wird beendet (PID: "+String.Join(", ",ids.ToArray())+")."));
  }));
  return answer;
 }
 void SetForceKill(Entry e) {
  e.ForceKill=true;
  try {
   var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
   Store.SetPrograms(copy,allPrograms);
   Store.SaveProfiles(copy);
   profiles=copy;
  } catch(Exception ex) {Log(e.Name+": Einstellung „ohne Rückfrage beenden“ konnte nicht gespeichert werden – "+ex.Message);}
 }
 // Prozesse eines Programms: gleicher Prozessname und IsOwnProcess (Ordner der EXE, eigene Sitzung, eigenes Konto).
 // Ohne eingetragene EXE oder lesbaren Pfad gilt ein Prozess nicht als eigener und wird nie beendet.
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(int access,bool inherit,int pid);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 static List<Process> OwnProcesses(Entry e,HashSet<string> skipped) {
  var result=new List<Process>();
  foreach(var p in Process.GetProcessesByName(e.ProcessName)) {
   bool mine=false;
   try {
    IntPtr h=OpenProcess(0x1000,false,p.Id); // PROCESS_QUERY_LIMITED_INFORMATION
    if(h!=IntPtr.Zero) try {mine=IsOwnProcess(h,p.Id,e);} finally {CloseHandle(h);}
   } catch {}
   if(mine)result.Add(p);
   else {if(skipped!=null)skipped.Add(p.Id.ToString());p.Dispose();}
  }
  return result;
 }

 // Nachkorrektur beim Start und nach Wiederherstellen: alle angehakten Kanten aus "Kanten je Programm"
 // mit ihren eigenen Abständen (wie "Automatisch"); ohne angehakte Kante bleibt die gespeicherte Position.
 string StartupRule(Entry e) {
  string edges=AutoEdges(e.Id);
  return edges.Length==0?null:WithGaps(e.Id,edges);
 }
 void Start(Entry e,bool afterRestore=false) {
  try {StartWithAlignment(e,afterRestore);}
  finally {
   if(afterRestore && e.RestartService!=null) {
    try {
     RestartService(e.RestartService);
     BeginInvoke((Action)(()=>Log(e.Name+": "+e.RestartService+" nach Wiederherstellen neu gestartet.")));
    } catch(Exception ex) {
     throw new Exception("Wiederherstellung bearbeitet; Neustart von "+e.RestartService+" fehlgeschlagen: "+ex.Message,ex);
    }
   }
  }
 }
 void StartWithAlignment(Entry e,bool afterRestore) {
  // Geprüfter Start (nur .exe, Pfad unmittelbar vorher geprüft; ab 2.68 gemeinsam mit RestoreWindow)
  Store.StartProgram(e);
  string rule=StartupRule(e);
  int gap=SavedGap();
  if(String.IsNullOrEmpty(rule))return;
  BeginInvoke((Action)(()=>Log(e.Name+": gestartet; Fensterkante wird waehrend des Starts nachkorrigiert …")));
  // Reapply through the loading phase: Electron can change bounds after its first window appears.
  var timer=Stopwatch.StartNew();
  string lastError="Kein fertiges Fenster gefunden.";
  bool success=false;
  Thread.Sleep(2000);
  while(timer.ElapsedMilliseconds<25000) {
   try {
    VisibleAlignment.AlignEntry(e,gap,Resolve(e.Id,rule,null));
    success=true;
    lastError=null;
    // Keep watching through at least twelve seconds of startup.
    if(timer.ElapsedMilliseconds>=12000) {
     Thread.Sleep(1500);
     var notes=new List<string>();
     string result=VisibleAlignment.AlignEntry(e,gap,Resolve(e.Id,rule,notes));
     if(notes.Count>0)result+="; "+String.Join("; ",notes.ToArray());
     if(e.RestartService!=null && !afterRestore)RestartService(e.RestartService);
     BeginInvoke((Action)(()=>Log(e.Name+": nach Start geprueft – "+result)));
     return;
    }
   } catch(Exception ex){lastError=ex.Message;success=false;}
   Thread.Sleep(1000);
  }
  throw new Exception("Programm gestartet; Ausrichtung nach Start nicht bestaetigt: "+(lastError??(success?"Startphase nicht abgeschlossen.":"Fenster nicht bereit.")));
 }
 // Gibt die Zahl der Fehler zurück (-1 = nichts ausgewählt).
 async Task<int> Run(bool capture) {
  // Nachbarprogramme zuerst bearbeiten, damit sich andere an ihrer neuen Lage ausrichten.
  var selected=OrderByNeighbors(list.Rows.Where(x=>x.Checked).ToList());
  if(selected.Count==0){MessageBox.Show(this,"Bitte mindestens ein Programm auswählen.",AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Information);return -1;}
  // Der eingestellte Abstand gilt auch für das Ausrichten nach dem Wiederherstellen.
  int gap=CurrentGap();
  SaveGap();
  SaveSelection(selected);
  SetBusy(true);
  int failures=0;
  try {
   foreach(var item in selected) {
    var entry=item.Entry; item.Status="Wird bearbeitet …";
    try {
     var profileCopy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
     await Task.Run(()=>{
      // Fensterprogramme (Easy2Sync, eigene) brauchen die Programmdatei nur, wenn sie gestartet werden müssen.
      if(!entry.WindowOnly) {
       // Pfadprüfung vor jedem Zugriff auf die Programmdatei (ab 2.68)
       string pathProblem=Store.ProgramPathProblem(entry);
       if(pathProblem!=null)throw new Exception(pathProblem+".");
       if(!Store.ProgramFileExists(entry))throw new FileNotFoundException("Programmdatei fehlt: "+entry.Exe);
      }
      if(!capture && !profileCopy.ContainsKey(entry.Id))throw new Exception("Keine Position gespeichert.");
      if(entry.WindowOnly) {
       // Fenster direkt messen bzw. verschieben; das Programm wird dafür nicht beendet.
       if(capture) {
        profileCopy[entry.Id]=new Dictionary<string,object>{{"Window",VisibleAlignment.CaptureWindow(entry)}};
        Store.SaveProfiles(profileCopy);profiles=profileCopy;
       } else {
        var saved=(Dictionary<string,object>)profileCopy[entry.Id];
        // z.B. TeamSpeak 3 bis 2.30: dort war statt der Fensterposition die ganze Datenbank gesichert.
        if(!saved.ContainsKey("Window"))throw new Exception("Keine Fensterposition gespeichert. Fenster öffnen und einmal „Positionen speichern“ anklicken.");
        string placed=VisibleAlignment.RestoreWindow(entry,(Dictionary<string,object>)saved["Window"]);
        BeginInvoke((Action)(()=>Log(entry.Name+": "+placed)));
        // Danach die angehakten Kanten aus "Kanten je Programm" ausrichten (wie "Automatisch").
        string rule=StartupRule(entry);
        if(!String.IsNullOrEmpty(rule)) {
         string aligned;
         var notes=new List<string>();
         try {aligned=VisibleAlignment.AlignEntry(entry,gap,Resolve(entry.Id,rule,notes));}
         catch(Exception alignError){throw new Exception("Fenster verschoben, Ausrichten fehlgeschlagen: "+alignError.Message);}
         if(notes.Count>0)aligned+="; "+String.Join("; ",notes.ToArray());
         BeginInvoke((Action)(()=>Log(entry.Name+": ausgerichtet – "+aligned)));
        }
        if(entry.CenterOthers) {
         // Weitere Fenster des Programms (z.B. Easy2Sync) waagerecht mittig über dem Hauptfenster (Höhe und senkrechte Lage bleiben).
         var extra=VisibleAlignment.CenterOthersOnMain(entry);
         BeginInvoke((Action)(()=>{foreach(string line in extra)Log(entry.Name+" – "+line);}));
        }
       }
       return;
      }
      bool running=Stop(entry);
      // "Immer ohne Rückfrage" kann die Programmliste geändert haben: in die Kopie übernehmen (sonst überschreibt das
      // Speichern unten die Einstellung).
      if(capture)Store.SetPrograms(profileCopy,allPrograms);
      try {
       if(capture) profileCopy[entry.Id]=Store.Capture(entry);
       else Store.Restore(entry,(Dictionary<string,object>)profileCopy[entry.Id]);
       if(capture) {Store.SaveProfiles(profileCopy);profiles=profileCopy;}
      } finally {if(capture && running)Start(entry);}
      if(!capture)Start(entry,true);
     });
     if(capture)profiles=profileCopy;
     item.Status=capture?"Position gespeichert":entry.WindowOnly?(StartupRule(entry)!=null?"Fenster verschoben und ausgerichtet":"Fenster verschoben"):"Wiederhergestellt und gestartet";
     Log(entry.Name+": "+item.Status);
    } catch(Exception ex){failures++;item.Status="Fehler – siehe Protokoll";Log(entry.Name+": "+ex.Message);}
   }
   Log("Fertig. "+(selected.Count-failures)+" erfolgreich, "+failures+" Fehler.");
  } finally {SetBusy(false);}
  return failures;
 }
 // Zuletzt bei "Wiederherstellen" bzw. "Positionen speichern" angehakte Programme (je Anordnung, "_auswahl"):
 // Diese stellt der Aufruf per Verknüpfung wieder her.
 void SaveSelection(IEnumerable<ProgramRow> rows) {
  if(DemoMode)return;
  try {
   var copy=Store.Json().Deserialize<Dictionary<string,object>>(Store.Json().Serialize(profiles));
   copy["_auswahl"]=rows.Select(x=>(object)x.Entry.Id).ToList();
   Store.SaveProfiles(copy);
   profiles=copy;
  } catch(Exception ex) {Log("Auswahl konnte nicht gespeichert werden: "+ex.Message);}
 }
 HashSet<string> AutoSelection() {
  var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var saved=profiles.ContainsKey("_auswahl")?profiles["_auswahl"] as System.Collections.IEnumerable:null;
  if(saved!=null&&!(saved is string))foreach(object id in saved)if(id!=null)ids.Add(Convert.ToString(id));
  // Ohne gespeicherte Auswahl: alle Programme mit gespeicherter Position.
  if(ids.Count==0)foreach(var e in entries)if(profiles.ContainsKey(e.Id))ids.Add(e.Id);
  ids.RemoveWhere(id=>!profiles.ContainsKey(id));
  return ids;
 }
 // ================================================================== Wiederherstellen per Verknüpfung
 async Task RunAutoRestore() {
  bool problem=false;
  string known=null;
  try {known=Store.Override==null?null:Store.FindArrangement(Store.LoadRaw(),Store.Override);} catch {}
  if(Store.Override!=null&&known==null) {
   Log("Die Anordnung „"+Store.Override+"“ gibt es nicht – es gilt „"+activeArrangement+"“.");
   problem=true;
  }
  var ids=AutoSelection();
  foreach(var row in list.Rows)row.Checked=ids.Contains(row.Entry.Id);
  UpdateSelection();
  if(ids.Count==0) {
   Log("In der Anordnung „"+activeArrangement+"“ sind keine Positionen gespeichert – nichts wiederhergestellt.");
   ShowAfterAutoRestore();
   return;
  }
  Log("Wiederherstellen per Verknüpfung – Anordnung „"+activeArrangement+"“.");
  int failures=await Run(false);
  if(failures==0&&!problem) {
   await Task.Delay(1500);
   Close();
  } else ShowAfterAutoRestore();
 }
 // Bei Fehlern das Fenster zeigen, damit das Protokoll sichtbar ist.
 void ShowAfterAutoRestore() {
  WindowState=FormWindowState.Normal;
  Activate();
 }
 // ================================================================== Anordnungen
 void ShowArrangementMenu() {
  if(busy)return;
  List<string> names;
  try {names=DemoMode?new List<string>{Store.StandardName,"Spielen"}:Store.ArrangementNames(Store.LoadRaw());}
  catch(Exception ex) {Log("Anordnungen konnten nicht gelesen werden: "+ex.Message);return;}
  MenuStyle.Prepare(arrangementMenu);
  arrangementMenu.Items.Add(MenuStyle.TopSpacer());
  foreach(string n in names) {
   string name=n;
   arrangementMenu.Items.Add(MenuStyle.Choice(name,name==activeArrangement,(s,a)=>SwitchArrangement(name)));
  }
  arrangementMenu.Items.Add(new ToolStripSeparator());
  arrangementMenu.Items.Add(MenuStyle.Item("Neue Anordnung …",'\uE710',(s,a)=>BeginInvoke((Action)NewArrangement)));
  bool own=activeArrangement!=Store.StandardName;
  var rename=MenuStyle.Item("Umbenennen …",'\uE8AC',(s,a)=>BeginInvoke((Action)RenameArrangement));
  rename.Enabled=own;
  arrangementMenu.Items.Add(rename);
  var delete=MenuStyle.Item("Löschen …",'\uE74D',(s,a)=>BeginInvoke((Action)DeleteArrangement));
  delete.Enabled=own;
  arrangementMenu.Items.Add(delete);
  arrangementMenu.Items.Add(new ToolStripSeparator());
  arrangementMenu.Items.Add(MenuStyle.Item("Verknüpfung auf dem Desktop anlegen",'\uE71B',(s,a)=>CreateShortcut()));
  arrangementMenu.Items.Add(MenuStyle.BottomSpacer());
  MenuStyle.ShowOnScreen(arrangementMenu,arrangement,new Point(0,arrangement.Height),ToolStripDropDownDirection.BelowRight);
 }
 // Nach jeder Änderung: Sicht der aktiven Anordnung neu laden (Positionen, Kanten, Abstand).
 void ReloadArrangement() {
  try {
   var raw=Store.LoadRaw();
   activeArrangement=Store.ActiveName(raw);
   profiles=Store.Load();
  } catch(Exception ex) {Log("Anordnung konnte nicht geladen werden: "+ex.Message);return;}
  arrangement.Text=activeArrangement;
  gapBox.Text=SavedGap().ToString();
  foreach(var row in list.Rows)row.Status="Bereit";
 }
 void SwitchArrangement(string name) {
  if(busy||DemoMode||name==activeArrangement)return;
  SaveGap();
  Store.Override=null;
  try {Store.SetActiveArrangement(name);}
  catch(Exception ex) {Log("Anordnung konnte nicht gewechselt werden: "+ex.Message);return;}
  ReloadArrangement();
  Log("Anordnung „"+activeArrangement+"“ aktiv.");
 }
 // Namen prüfen: nicht leer, keine Zeichen, die in Dateinamen (Verknüpfung) verboten sind, nicht doppelt.
 Func<string,string> ArrangementNameCheck(string current) {
  return text=>{
   if(text.Length==0)return "Bitte einen Namen eingeben.";
   if(text.IndexOfAny("\"\\/:*?<>|".ToCharArray())>=0)return "Bitte ohne die Zeichen \" \\ / : * ? < > |";
   if(text.StartsWith("_")||text.StartsWith("-"))return "Der Name darf nicht mit _ oder - beginnen.";
   string found;
   try {found=Store.FindArrangement(Store.LoadRaw(),text);} catch(Exception ex) {return ex.Message;}
   if(found!=null&&found!=current)return "„"+found+"“ gibt es schon.";
   return null;
  };
 }
 void NewArrangement() {
  if(busy||DemoMode)return;
  string source=activeArrangement;
  string name=InputDialog.Ask(this,"Neue Anordnung","Sie übernimmt Positionen und Kanten von „"+source+"“. Danach die Fenster anordnen und „Positionen speichern“ anklicken.","","Anlegen",ArrangementNameCheck(null));
  if(name==null)return;
  SaveGap();
  Store.Override=null;
  try {Store.CreateArrangement(name,profiles);}
  catch(Exception ex) {Log("Anordnung konnte nicht angelegt werden: "+ex.Message);return;}
  ReloadArrangement();
  Log("Neue Anordnung „"+name+"“ angelegt (Kopie von „"+source+"“) und aktiv.");
 }
 void RenameArrangement() {
  string old=activeArrangement;
  if(busy||DemoMode||old==Store.StandardName)return;
  string name=InputDialog.Ask(this,"Anordnung umbenennen",null,old,"Umbenennen",ArrangementNameCheck(old));
  if(name==null||name==old)return;
  Store.Override=null;
  try {Store.RenameArrangement(old,name);}
  catch(Exception ex) {Log("Anordnung konnte nicht umbenannt werden: "+ex.Message);return;}
  ReloadArrangement();
  Log("Anordnung „"+old+"“ heißt jetzt „"+name+"“. Verknüpfungen mit dem alten Namen bitte neu anlegen.");
 }
 void DeleteArrangement() {
  string name=activeArrangement;
  if(busy||DemoMode||name==Store.StandardName)return;
  if(MessageBox.Show(this,"Die gespeicherten Positionen und Kanten dieser Anordnung werden gelöscht; die Fenster selbst bleiben, wie sie sind. Danach ist „"+Store.StandardName+"“ aktiv.\r\n\r\nAnordnung „"+name+"“ löschen?","Anordnung löschen",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
  Store.Override=null;
  try {Store.DeleteArrangement(name);}
  catch(Exception ex) {Log("Anordnung konnte nicht gelöscht werden: "+ex.Message);return;}
  ReloadArrangement();
  Log("Anordnung „"+name+"“ gelöscht – „"+activeArrangement+"“ ist aktiv.");
 }
 // Verknüpfung, die die aktive Anordnung ohne Programmfenster wiederherstellt (z.B. für den Autostart-Ordner).
 void CreateShortcut() {
  string name=activeArrangement;
  string title="Fenster wiederherstellen"+(name==Store.StandardName?"":" ("+name+")");
  string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),title+".lnk");
  object shell=null,link=null;
  try {
   Type type=Type.GetTypeFromProgID("WScript.Shell");
   shell=Activator.CreateInstance(type);
   link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
   Type linkType=link.GetType();
   linkType.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{Application.ExecutablePath});
   linkType.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{"--wiederherstellen"+(name==Store.StandardName?"":" "+Quote(name))});
   linkType.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(Application.ExecutablePath)});
   linkType.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{Application.ExecutablePath+",0"});
   linkType.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{"Gespeicherte Fensterpositionen wiederherstellen (Anordnung "+name+")"});
   linkType.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
   Log("Verknüpfung „"+title+"“ auf dem Desktop angelegt: Sie stellt die Anordnung „"+name+"“ wieder her, ohne das Programmfenster zu öffnen.");
  } catch(Exception ex) {
   MessageBox.Show(this,"Die Verknüpfung konnte nicht angelegt werden: "+ex.Message,AppInfo.Product,MessageBoxButtons.OK,MessageBoxIcon.Warning);
  } finally {
   if(link!=null)Marshal.FinalReleaseComObject(link);
   if(shell!=null)Marshal.FinalReleaseComObject(shell);
  }
 }
}
}
