// Fensterpositionen wiederherstellen – Info-Fenster und Dialog „Programm hinzufügen“
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
public class AboutForm:FixedSizeForm {
 public AboutForm(Bitmap logo,Icon icon,EventHandler openBackups,UpdateHooks updates) {
  Text="Info über "+AppInfo.Product;
  // Normaler Rahmen wie die Hauptfenster (Icon mit gleichem Abstand), Größe fest: siehe FixedSizeForm.
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(540),pad=Ui.Px(32),y=Ui.Px(16);
  if(logo!=null) {
   Controls.Add(new LogoView{Logo=logo,BackColor=Ui.Bg,Bounds=new Rectangle(pad,y,w-2*pad,Ui.Px(150))});
   y+=Ui.Px(158);
  }
  Controls.Add(new Label{Text="Fensterpositionen wiederherstellen",Font=Ui.HeadingFont,ForeColor=Ui.TextColor,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(34))});
  y+=Ui.Px(34);
  Controls.Add(new Label{Text="Version "+AppInfo.Version,Font=Ui.SmallFont,ForeColor=Ui.Accent,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(24))});
  y+=Ui.Px(36);
  string built="";int year=DateTime.Now.Year;
  try{DateTime stamp=System.IO.File.GetLastWriteTime(Application.ExecutablePath);built=stamp.ToString("dd.MM.yyyy");year=stamp.Year;}catch{}
  string root=Store.Root;
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  if(root.StartsWith(local,StringComparison.OrdinalIgnoreCase))root="%LOCALAPPDATA%"+root.Substring(local.Length);
  var lines=new[]{new[]{"Entwickler","Günther Schoder"},new[]{"Erstellt",built},new[]{"Farbschema",(Ui.Dark?"dunkel":"hell")+" (folgt Windows)"},new[]{"Positionen",root}};
  int lineH=Ui.Px(26),updateH=updates!=null?Ui.Px(60)+Ui.Px(32):0,cardH=lines.Length*lineH+Ui.Px(24)+updateH;
  var card=new Card{Bounds=new Rectangle(pad,y,w-2*pad,cardH),Padding=new Padding(0)};
  for(int i=0;i<lines.Length;i++) {
   card.Controls.Add(new Label{Text=lines[i][0],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),Ui.Px(12)+i*lineH,Ui.Px(96),lineH)});
   card.Controls.Add(new Label{Text=lines[i][1],Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(112),Ui.Px(12)+i*lineH,card.Width-Ui.Px(124),lineH)});
  }
  if(updates!=null)AddUpdateRows(card,Ui.Px(12)+lines.Length*lineH,updates);
  Controls.Add(card);
  y+=cardH+Ui.Px(18);
  string guide=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Anleitung.txt");
  var guideButton=new ModernButton("Anleitung",ButtonKind.Secondary,"\uE8A5");
  guideButton.Enabled=System.IO.File.Exists(guide);
  guideButton.Click+=(s,e)=>{try{Process.Start(guide);}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
  var backupButton=new ModernButton("Sicherungen",ButtonKind.Secondary,"\uE838");
  backupButton.Click+=openBackups;
  var traceButton=new ModernButton("Protokolle",ButtonKind.Secondary,"\uE81C");
  traceButton.Click+=(s,e)=>{
   try{Directory.CreateDirectory(TraceLog.Folder);Process.Start("explorer.exe","\""+TraceLog.Folder+"\"");}
   catch(Exception ex){MessageBox.Show(this,ex.Message);}
  };
  var closeButton=new ModernButton("Schließen",ButtonKind.Primary,null);
  closeButton.DialogResult=DialogResult.OK;
  var buttons=new[]{guideButton,backupButton,traceButton,closeButton};
  int total=0;foreach(var b in buttons)total+=b.Width;total+=Ui.Px(8)*(buttons.Length-1);
  int bx=(w-total)/2;
  foreach(var b in buttons){b.Location=new Point(bx,y);bx+=b.Width+Ui.Px(8);Controls.Add(b);}
  AcceptButton=closeButton;CancelButton=closeButton;
  y+=Ui.Px(38)+Ui.Px(16);
  Controls.Add(new Label{Text="© "+year+" Günther Schoder",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,TextAlign=ContentAlignment.MiddleCenter,Bounds=new Rectangle(0,y,w,Ui.Px(22))});
  y+=Ui.Px(22)+Ui.Px(18);
  ClientSize=new Size(w,y);
 }
 // Updates (Globale Richtlinien): Zeile "Updates" 60 hoch mit Symbol, Stand, "Zuletzt geprüft …" und Knopf,
 // darunter 32 hoch "Automatisch prüfen" mit Schalter.
 static Font detailFont,smallGlyphFont;
 GlyphView statusGlyph;Label statusText,statusDetail;ModernButton statusButton;
 UpdateHooks hooks;Card updateCard;
 enum ButtonMode {Check,Install,Page}
 ButtonMode mode;
 void AddUpdateRows(Card card,int top,UpdateHooks updates) {
  hooks=updates;updateCard=card;
  if(detailFont==null)detailFont=new Font("Segoe UI",8.75f);
  if(smallGlyphFont==null&&Ui.IconFont!=null)smallGlyphFont=new Font(Ui.IconFont.FontFamily,10f);
  int y1=top+Ui.Px(9),y2=top+Ui.Px(33),lineH=Ui.Px(22);
  card.Controls.Add(new Label{Text="Updates",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(16),y1,Ui.Px(96),lineH)});
  statusGlyph=new GlyphView{BackColor=Ui.Surface,Bounds=new Rectangle(Ui.Px(110),y1,Ui.Px(20),lineH)};
  statusButton=new ModernButton("Jetzt prüfen",ButtonKind.Secondary,"\uE72C"){Height=Ui.Px(30),Margin=new Padding(0),GlyphFont=smallGlyphFont};
  statusText=new Label{Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft};
  statusText.Location=new Point(Ui.Px(136),y1);statusText.Height=lineH;
  statusDetail=new Label{Font=detailFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(136),y2,card.Width-Ui.Px(136)-Ui.Px(16),Ui.Px(20))};
  statusButton.Click+=async(s,e)=>await StatusButtonClick();
  card.Controls.Add(statusGlyph);card.Controls.Add(statusText);card.Controls.Add(statusDetail);card.Controls.Add(statusButton);
  // Knopf vor die volle Breite der zweiten Zeile legen, sonst verdeckt sie seinen unteren Rand.
  statusButton.BringToFront();
  // Zweite Zeile: nur "Automatisch prüfen", bündig mit den Werten; Schalter rechtsbündig mit dem Knopf.
  int top2=top+Ui.Px(60);
  var toggle=new ToggleSwitch{Checked=updates.Auto(),BackColor=Ui.Surface};
  toggle.Location=new Point(card.Width-Ui.Px(16)-toggle.Width,top2+(Ui.Px(32)-toggle.Height)/2);
  toggle.CheckedChanged+=(s,e)=>updates.SetAuto(toggle.Checked);
  var autoText=new Label{Text="Automatisch prüfen",Font=Ui.SmallFont,ForeColor=Ui.TextColor,BackColor=Ui.Surface,Cursor=Cursors.Hand,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(Ui.Px(112),top2,toggle.Left-Ui.Px(120),Ui.Px(32))};
  autoText.Click+=(s,e)=>toggle.Checked=!toggle.Checked;
  card.Controls.Add(autoText);card.Controls.Add(toggle);
  ShowStatus(updates.Status());
 }
 void ShowStatus(UpdateStatus st) {
  string glyph;Color color=Ui.Muted,textColor=Ui.TextColor;
  switch(st.State) {
   case UpdateState.Current:glyph="\uE930";color=Ui.SuccessColor;break;
   case UpdateState.Available:glyph="\uE896";color=Ui.Accent;textColor=Ui.Accent;break;
   case UpdateState.Error:glyph="\uE7BA";color=Ui.ErrorColor;break;
   case UpdateState.Checking:glyph="\uE895";break;
   default:glyph="\uE946";break;
  }
  statusGlyph.Glyph=glyph;statusGlyph.GlyphColor=color;
  statusText.Text=st.Text;statusText.ForeColor=textColor;
  statusDetail.Text=st.Detail??"";
  if(st.State==UpdateState.Available&&hooks.CanInstall!=null&&hooks.CanInstall()) {
   mode=ButtonMode.Install;statusButton.Text="Aktualisieren";statusButton.Kind=ButtonKind.Primary;statusButton.Glyph="\uE896";
  } else if(st.State==UpdateState.Available) {
   mode=ButtonMode.Page;statusButton.Text="Release-Seite";statusButton.Kind=ButtonKind.Secondary;statusButton.Glyph="\uE71B";
  } else {
   mode=ButtonMode.Check;statusButton.Text="Jetzt prüfen";statusButton.Kind=ButtonKind.Secondary;statusButton.Glyph="\uE72C";
  }
  statusButton.Enabled=st.State!=UpdateState.Checking;
  // Knopf auf Höhe der ersten Zeile, rechtsbündig; der Stand bekommt den Platz links davon.
  statusButton.Location=new Point(updateCard.Width-Ui.Px(16)-statusButton.Width,statusText.Top+(statusText.Height-statusButton.Height)/2);
  statusText.Width=Math.Max(Ui.Px(40),statusButton.Left-Ui.Px(8)-statusText.Left);
 }
 async Task StatusButtonClick() {
  if(mode==ButtonMode.Install){Close();if(hooks.Install!=null)hooks.Install();return;}
  if(mode==ButtonMode.Page){if(hooks.OpenPage!=null)hooks.OpenPage();return;}
  ShowStatus(new UpdateStatus{State=UpdateState.Checking,Text="Prüfe auf GitHub …",Detail=statusDetail.Text});
  UpdateStatus result;
  try {result=await hooks.CheckNow();}
  catch(Exception ex){result=new UpdateStatus{State=UpdateState.Error,Text="Prüfung nicht möglich",Detail=ex.Message};}
  if(!IsDisposed)ShowStatus(result);
 }
}

// Stand der Update-Prüfung für das Info-Fenster.
public enum UpdateState {Unknown,Current,Available,Error,Checking}
public class UpdateStatus {
 public UpdateState State;
 public string Text,Detail;
}
// Verbindung des Info-Fensters zur Update-Prüfung im Hauptfenster (Globale Richtlinien, Abschnitt "Updates").
public class UpdateHooks {
 public Func<UpdateStatus> Status;
 public Func<bool> Auto;
 public Action<bool> SetAuto;
 public Func<Task<UpdateStatus>> CheckNow;
 public Func<bool> CanInstall;
 public Action Install;
 public Action OpenPage;
}

// Seite "Programm hinzufügen" (im Hauptfenster eingebettet, Überschrift kommt von dort):
// listet geöffnete Fenster; gespeichert wird nur die Fensterposition.
public class AddProgramForm:FixedSizeForm {
 class WindowChoice {
  public string Title,Process,Exe,Description;
 }
 readonly ListBox windows=new ListBox();
 readonly TextBox nameBox=new TextBox();
 readonly ModernButton addButton;
 readonly HashSet<string> excluded;
 readonly HashSet<string> usedIds;
 public Entry Result;
 public AddProgramForm(Icon icon,IEnumerable<Entry> existing) {
  excluded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  usedIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in existing) {
   if(!String.IsNullOrEmpty(entry.ProcessName))excluded.Add(entry.ProcessName);
   if(!String.IsNullOrEmpty(entry.AltProcessName))excluded.Add(entry.AltProcessName);
   usedIds.Add(entry.Id);
  }
  Text="Programm hinzufügen";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(640),pad=Ui.Px(24),y=Ui.Px(4),inner=w-2*pad;
  var intro=new Label{Text="Wähle ein geöffnetes Fenster. Gespeichert wird nur seine Position und Größe – das Programm wird dafür nicht beendet.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(40))};
  Controls.Add(intro);
  y+=Ui.Px(46);
  var listCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(268)),Padding=new Padding(Ui.Px(4))};
  windows.Dock=DockStyle.Fill;windows.BorderStyle=BorderStyle.None;windows.IntegralHeight=false;
  windows.DrawMode=DrawMode.OwnerDrawFixed;windows.ItemHeight=Ui.Px(44);
  windows.BackColor=Ui.Surface;windows.ForeColor=Ui.TextColor;
  windows.DrawItem+=DrawWindow;
  windows.HandleCreated+=(s,e)=>Native.DarkScroll(windows);
  windows.SelectedIndexChanged+=(s,e)=>{
   var choice=windows.SelectedItem as WindowChoice;
   if(choice!=null)nameBox.Text=String.IsNullOrEmpty(choice.Description)?choice.Process:choice.Description;
   UpdateButtons();
  };
  windows.DoubleClick+=(s,e)=>{if(windows.SelectedItem!=null)Accept();};
  listCard.Controls.Add(windows);
  Controls.Add(listCard);
  y+=listCard.Height+Ui.Px(14);
  var nameLabel=new Label{Text="Name in der Liste",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(22))};
  Controls.Add(nameLabel);
  y+=Ui.Px(24);
  var nameCard=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(36)),Padding=new Padding(Ui.Px(10),Ui.Px(8),Ui.Px(10),0)};
  nameBox.Dock=DockStyle.Fill;nameBox.BorderStyle=BorderStyle.None;nameBox.BackColor=Ui.Surface;nameBox.ForeColor=Ui.TextColor;nameBox.Font=Ui.UiFont;
  nameBox.TextChanged+=(s,e)=>UpdateButtons();
  nameCard.Controls.Add(nameBox);
  Controls.Add(nameCard);
  y+=nameCard.Height+Ui.Px(20);
  var refresh=new ModernButton("Aktualisieren",ButtonKind.Secondary,"");
  refresh.Location=new Point(pad,y);
  refresh.Click+=(s,e)=>LoadWindows();
  Controls.Add(refresh);
  addButton=new ModernButton("Hinzufügen",ButtonKind.Primary,null);
  var cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  // Wie bei den Einstellungsseiten: "Abbrechen" links, Primärknopf ganz rechts, 10 Abstand.
  addButton.Location=new Point(w-pad-addButton.Width,y);
  cancel.Location=new Point(addButton.Left-Ui.Px(10)-cancel.Width,y);
  addButton.Click+=(s,e)=>Accept();
  Controls.Add(cancel);Controls.Add(addButton);
  CancelButton=cancel;
  y+=Ui.Px(38)+Ui.Px(20);
  ClientSize=new Size(w,y);
  // Im Hauptfenster füllt die Seite den Inhaltsbereich: Liste wächst mit, Name und Knöpfe bleiben unten.
  // (Anker erst nach der endgültigen Startgröße setzen, sonst stimmen die Abstände nicht.)
  intro.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  listCard.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  nameLabel.Anchor=nameCard.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
  refresh.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  addButton.Anchor=cancel.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
  LoadWindows();
 }
 void UpdateButtons(){addButton.Enabled=windows.SelectedItem!=null&&nameBox.Text.Trim().Length>0;}
 void LoadWindows() {
  var choices=new List<WindowChoice>();
  if(MainForm.DemoMode) {
   // Screenshots: feste Demo-Liste statt der tatsächlich offenen Fenster (deren Titel privat sein können).
   choices.Add(new WindowChoice{Title="Downloads – Datei-Explorer",Process="explorer",Exe=@"C:\Windows\explorer.exe",Description="Windows-Explorer"});
   choices.Add(new WindowChoice{Title="Paint",Process="mspaint",Exe=@"C:\Windows\System32\mspaint.exe",Description="Paint"});
   choices.Add(new WindowChoice{Title="Rechner",Process="CalculatorApp",Exe=@"C:\Program Files\WindowsApps\CalculatorApp.exe",Description="Rechner"});
   choices.Add(new WindowChoice{Title="Terminal",Process="WindowsTerminal",Exe=@"C:\Program Files\WindowsApps\WindowsTerminal.exe",Description="Terminal"});
   choices.Add(new WindowChoice{Title="Unbenannt – Editor",Process="Notepad",Exe=@"C:\Windows\System32\notepad.exe",Description="Editor"});
   windows.BeginUpdate();windows.Items.Clear();
   foreach(var c in choices)windows.Items.Add(c);
   windows.EndUpdate();
   windows.SelectedIndex=choices.Count-1;
   UpdateButtons();
   return;
  }
  int own=Process.GetCurrentProcess().Id;
  foreach(var process in Process.GetProcesses()) using(process) {
   try {
    if(process.Id==own||process.MainWindowHandle==IntPtr.Zero)continue;
    string title=process.MainWindowTitle;
    if(String.IsNullOrEmpty(title)||excluded.Contains(process.ProcessName))continue;
    var choice=new WindowChoice{Title=title,Process=process.ProcessName};
    // Pfad und Beschreibung sind bei Programmen mit Administratorrechten ohne eigene Adminrechte nicht lesbar.
    try {
     choice.Exe=process.MainModule.FileName;
     choice.Description=FileVersionInfo.GetVersionInfo(choice.Exe).FileDescription;
     if(choice.Description!=null) {
      choice.Description=choice.Description.Trim();
      // Manche Programme (z.B. der Windows-11-Editor) tragen nur den Dateinamen ein: "Notepad.exe" -> "Notepad".
      if(choice.Description.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))choice.Description=choice.Description.Substring(0,choice.Description.Length-4);
     }
    } catch {}
    choices.Add(choice);
   } catch {}
  }
  choices.Sort((a,b)=>String.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase));
  windows.BeginUpdate();
  windows.Items.Clear();
  foreach(var c in choices)windows.Items.Add(c);
  windows.EndUpdate();
  if(windows.Items.Count>0)windows.SelectedIndex=0;
  else nameBox.Text="";
  UpdateButtons();
 }
 void DrawWindow(object sender,DrawItemEventArgs e) {
  var g=e.Graphics;
  using(var back=new SolidBrush(Ui.Surface)) g.FillRectangle(back,e.Bounds);
  if(e.Index<0||e.Index>=windows.Items.Count)return;
  var choice=(WindowChoice)windows.Items[e.Index];
  bool selected=(e.State&DrawItemState.Selected)==DrawItemState.Selected;
  g.SmoothingMode=SmoothingMode.AntiAlias;
  var r=Rectangle.Inflate(e.Bounds,-Ui.Px(2),-Ui.Px(2));
  if(selected) {
   using(var path=Ui.Round(r,Ui.Px(4)))
   using(var brush=new SolidBrush(Ui.Blend(Ui.Surface,Ui.Accent,0.16f))) g.FillPath(brush,path);
   // Schmale Akzentmarke links wie in Windows-11-Listen.
   using(var path=Ui.Round(new Rectangle(r.X+Ui.Px(2),r.Y+r.Height/2-Ui.Px(8),Ui.Px(3),Ui.Px(16)),Ui.Px(1)))
   using(var brush=new SolidBrush(Ui.Accent)) g.FillPath(brush,path);
  }
  var flags=TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.Left|TextFormatFlags.NoPadding;
  int x=r.X+Ui.Px(14),width=r.Width-Ui.Px(24);
  TextRenderer.DrawText(g,choice.Title,Ui.UiSemibold,new Rectangle(x,r.Y+Ui.Px(4),width,Ui.Px(20)),Ui.TextColor,flags);
  string detail=choice.Process+".exe"+(String.IsNullOrEmpty(choice.Description)?"":" · "+choice.Description)+(choice.Exe==null?" · Pfad nicht lesbar (Administrator?)":"");
  TextRenderer.DrawText(g,detail,Ui.SmallFont,new Rectangle(x,r.Y+Ui.Px(22),width,Ui.Px(18)),Ui.Muted,flags);
 }
 void Accept() {
  var choice=windows.SelectedItem as WindowChoice;
  string name=nameBox.Text.Trim();
  if(choice==null||name.Length==0)return;
  var id=new StringBuilder("eigen-");
  foreach(char c in choice.Process.ToLowerInvariant())id.Append(Char.IsLetterOrDigit(c)?c:'-');
  string baseId=id.ToString(),unique=baseId;
  for(int i=2;usedIds.Contains(unique);i++)unique=baseId+"-"+i;
  Result=new Entry{Id=unique,Name=name,ProcessName=choice.Process,Exe=choice.Exe,WindowOnly=true,Custom=true,Keys=new[]{"Window"}};
  DialogResult=DialogResult.OK;
  Close();
 }
}

// Seite "Einmalig ausrichten" (im Hauptfenster eingebettet): ein beliebiges offenes Fenster anklicken und ausrichten,
// ohne es in die Liste aufzunehmen. Es wird nichts gespeichert (keine Regel, keine Nachkorrektur).
public class OneTimeAlignForm:FixedSizeForm {
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,StringBuilder name,int max);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
 // Desktop, Taskleiste, Startmenü und Infobereich zählen nicht als Fenster.
 static readonly string[] IgnoredClasses={"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd","Windows.UI.Core.CoreWindow","NotifyIconOverflowWindow","TopLevelWindowForOverflowXamlIsland","XamlExplorerHostIslandWindow"};
 readonly Func<int> gap;
 readonly Action<string> log;
 readonly Label windowTitle,windowDetail,status;
 readonly ModernButton pick;
 readonly List<ModernButton> directions=new List<ModernButton>();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=150};
 readonly uint ownProcess=(uint)Process.GetCurrentProcess().Id;
 IntPtr target=IntPtr.Zero;
 string targetName="";
 bool picking,working;
 public OneTimeAlignForm(Icon icon,Func<int> gap,Action<string> log) {
  this.gap=gap;this.log=log;
  Text="Einmalig ausrichten";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  KeyPreview=true;
  int w=Ui.Px(640),pad=Ui.Px(24),y=Ui.Px(4),inner=w-2*pad;
  var intro=new Label{Text="Ein beliebiges offenes Fenster anklicken und ausrichten. Es wird nicht in die Liste aufgenommen und nichts gespeichert.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",Bounds=new Rectangle(pad,y,inner,Ui.Px(40))};
  Controls.Add(intro);
  y+=Ui.Px(48);
  var card=new Card{Bounds=new Rectangle(pad,y,inner,Ui.Px(64))};
  pick=new ModernButton("Fenster anklicken",ButtonKind.Primary,"\uE7C9");
  pick.Location=new Point(card.Width-Ui.Px(13)-pick.Width,(card.Height-pick.Height)/2);
  pick.Click+=(s,e)=>StartPick();
  int textWidth=pick.Left-Ui.Px(28);
  windowTitle=new Label{Text="Noch kein Fenster gewählt",Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,Bounds=new Rectangle(Ui.Px(16),Ui.Px(10),textWidth,Ui.Px(22))};
  windowDetail=new Label{Text="„Fenster anklicken“ und danach auf das gewünschte Fenster klicken",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",AutoEllipsis=true,UseMnemonic=false,Bounds=new Rectangle(Ui.Px(16),Ui.Px(33),textWidth,Ui.Px(20))};
  card.Controls.Add(windowTitle);card.Controls.Add(windowDetail);card.Controls.Add(pick);
  Controls.Add(card);
  y+=card.Height+Ui.Px(16);
  string[] modes={"top","bottom","left","right","hcenter","vcenter"};
  string[] names={"Oben","Unten","Links","Rechts","↔ Mitte","↕ Mitte"};
  string[] glyphs={"\uE74A","\uE74B","\uE72B","\uE72A",null,null};
  int x=pad;
  for(int i=0;i<modes.Length;i++) {
   var button=new ModernButton(names[i],ButtonKind.Secondary,glyphs[i]);
   string mode=modes[i],name=names[i];
   button.Location=new Point(x,y);
   button.Click+=async(s,e)=>await Apply(mode,name);
   x+=button.Width+Ui.Px(6);
   directions.Add(button);Controls.Add(button);
  }
  y+=Ui.Px(38)+Ui.Px(12);
  status=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",AutoEllipsis=true,UseMnemonic=false,Bounds=new Rectangle(pad,y,inner,Ui.Px(40))};
  Controls.Add(status);
  // Kein Knopf "Schließen": zurück geht es über die Navigation (oder Esc).
  y+=status.Height+Ui.Px(20);
  ClientSize=new Size(w,y);
  // Im Hauptfenster nutzt die Seite die volle Breite (Anker erst nach der Startgröße setzen).
  intro.Anchor=card.Anchor=status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  pick.Anchor=AnchorStyles.Top|AnchorStyles.Right;
  windowTitle.Anchor=windowDetail.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  timer.Tick+=(s,e)=>CheckForeground();
  FormClosed+=(s,e)=>{timer.Stop();timer.Dispose();};
  UpdateButtons();
 }
 // Vom Hauptfenster nach dem Anzeigen aufgerufen: gleich auf den Klick warten, meist ist genau das gewünscht.
 public void Begin(){StartPick();}
 protected override void OnKeyDown(KeyEventArgs e) {
  base.OnKeyDown(e);
  if(e.KeyCode!=Keys.Escape)return;
  e.Handled=true;
  if(picking){StopPick();status.Text="Auswahl abgebrochen.";}
  else Close();
 }
 void UpdateButtons() {
  bool ready=target!=IntPtr.Zero&&!picking&&!working;
  foreach(var button in directions)button.Enabled=ready;
  pick.Enabled=!picking&&!working;
 }
 void StartPick() {
  if(working)return;
  picking=true;
  status.Text="Jetzt auf das gewünschte Fenster klicken … (Esc = abbrechen)";
  timer.Start();
  UpdateButtons();
 }
 void StopPick(){picking=false;timer.Stop();UpdateButtons();}
 // Das angeklickte Fenster wird zum Vordergrundfenster; eigene Fenster, Desktop und Taskleiste werden übergangen.
 void CheckForeground() {
  IntPtr handle=GetForegroundWindow();
  if(handle==IntPtr.Zero)return;
  IntPtr root=GetAncestor(handle,2);
  if(root!=IntPtr.Zero)handle=root;
  uint process;
  GetWindowThreadProcessId(handle,out process);
  if(process==ownProcess||!IsWindowVisible(handle))return;
  var cls=new StringBuilder(256);
  GetClassName(handle,cls,cls.Capacity);
  if(IgnoredClasses.Contains(cls.ToString()))return;
  var title=new StringBuilder(512);
  GetWindowText(handle,title,title.Capacity);
  string processName="";
  try {using(var p=Process.GetProcessById((int)process))processName=p.ProcessName+".exe";} catch {}
  target=handle;
  targetName=title.Length>0?title.ToString():(processName.Length>0?processName:"Fenster");
  windowTitle.Text=targetName;
  windowDetail.Text=processName.Length>0?processName:"Prozess nicht lesbar";
  StopPick();
  status.Text="Fenster gewählt – jetzt die Richtung anklicken. Mehrere Richtungen nacheinander ergänzen sich.";
  // Das angeklickte Fenster liegt jetzt vorn: Programmfenster wieder nach vorn holen, damit die Richtungen erreichbar sind.
  var top=TopLevelControl as Form;
  if(top!=null)top.Activate();
 }
 async Task Apply(string mode,string name) {
  if(target==IntPtr.Zero||working||picking)return;
  if(!IsWindow(target)) {
   target=IntPtr.Zero;windowTitle.Text="Noch kein Fenster gewählt";windowDetail.Text="Das Fenster ist nicht mehr geöffnet";
   status.Text="Bitte erneut „Fenster anklicken“.";UpdateButtons();return;
  }
  working=true;UpdateButtons();
  IntPtr handle=target;int pixels=gap();
  status.Text=name+" …";
  try {
   string result=await Task.Run(()=>VisibleAlignment.AlignHandle(handle,pixels,mode));
   status.Text=result;
   log("Einmalig ausrichten – "+targetName+": "+result);
  } catch(Exception ex) {
   status.Text=ex.Message;
   log("Einmalig ausrichten – "+targetName+": "+ex.Message);
  } finally {working=false;UpdateButtons();}
 }
}

// Einstellungsseite "Kanten je Programm" (im Hauptfenster eingebettet): je Programm die Kanten festlegen,
// an denen ausgerichtet wird. Übernommen wird erst mit "Speichern"; "Abbrechen" oder ein Wechsel über die Navigation verwirft.
public class AlignRulesForm:FixedSizeForm {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,string lParam);
 static readonly string[] Directions={"top","bottom","left","right","hcenter","vcenter"};
 static readonly string[] Titles={"Oben","Unten","Links","Rechts","↔ Mitte","↕ Mitte"};
 // Nur Oben/Unten/Links/Rechts haben einen eigenen Abstand; bei Mitte wird gleich verteilt.
 const int EdgeCount=4;
 readonly List<Entry> programs;
 readonly Dictionary<string,CheckCell[]> cells=new Dictionary<string,CheckCell[]>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,TextBox[]> boxes=new Dictionary<string,TextBox[]>(StringComparer.OrdinalIgnoreCase);
 // Nachbarprogramm je Kante (null = Bildschirmrand) und die Beschriftung darunter.
 readonly Dictionary<string,string[]> refs=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Label[]> refLabels=new Dictionary<string,Label[]>(StringComparer.OrdinalIgnoreCase);
 readonly ContextMenuStrip refMenu=new ContextMenuStrip();
 readonly Label status,error;
 // Speichern (Globale Richtlinien, Einstellungsseiten): liefert eine Fehlermeldung oder null; bei Fehler bleibt der Dialog offen.
 public Func<string> Save;
 public Dictionary<string,HashSet<string>> Rules;
 public Dictionary<string,Dictionary<string,int>> Gaps;
 public Dictionary<string,Dictionary<string,string>> Refs;
 // Seitenaufbau: Spalten und Knöpfe passen sich an die Breite an (Arrange), die Tabelle an die Höhe.
 readonly Label description;
 readonly Card card;
 readonly Panel rows,header;
 readonly ModernButton standard,ok,cancel;
 readonly List<Action> placers=new List<Action>();
 readonly int head,rowH;
 int[] colW=new int[6],colLeft=new int[6];
 int colX,measureW;
 public AlignRulesForm(Icon icon,IEnumerable<Entry> list,Dictionary<string,HashSet<string>> current,Dictionary<string,Dictionary<string,int>> currentGaps,Dictionary<string,Dictionary<string,string>> currentRefs,int globalGap) {
  programs=new List<Entry>(list);
  Refs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  refMenu.Opened+=(s,e)=>MenuStyle.RoundCorners(refMenu);
  Rules=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  Gaps=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  Text="Kanten je Programm";
  StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.None;
  Font=Ui.UiFont;BackColor=Ui.Bg;ForeColor=Ui.TextColor;Icon=icon;
  int w=Ui.Px(944),pad=Ui.Px(24),inner=w-2*pad;
  head=Ui.Px(36);rowH=Ui.Px(64);
  description=new Label{Text="Angehakte Kanten werden ausgerichtet, nicht angehakte bleiben, wie sie sind. Neben dem Haken steht der Abstand in px: leer = allgemeiner Abstand ("+globalGap+" px), sonst gilt die Zahl nur für dieses Programm. Darunter steht, wovon der Abstand gilt: vom Bildschirmrand oder von einem Nachbarprogramm (anklicken zum Ändern). „Messen“ übernimmt die aktuellen Abstände des offenen Fensters für die angehakten Kanten.\r\n„Automatisch“ richtet alle angehakten Kanten zusammen aus (Oben + Unten = volle Höhe, Links + Rechts = volle Breite). Eigene Programme haben keine Vorgabe.",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted"};
  Controls.Add(description);
  // Höchstens 8 Zeilen, auf kleinen Bildschirmen weniger (darüber wird in der Tabelle gescrollt).
  int fitRows=(Screen.PrimaryScreen.WorkingArea.Height-Ui.Px(540))/rowH;
  int visibleRows=Math.Max(3,Math.Min(programs.Count,Math.Min(8,fitRows)));
  card=new Card{Bounds=new Rectangle(pad,0,inner,head+visibleRows*rowH+Ui.Px(10)),Padding=new Padding(Ui.Px(1))};
  header=new Panel{Dock=DockStyle.Top,Height=head,BackColor=Ui.Surface};
  var programHead=new Label{Text="Programm",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=ContentAlignment.MiddleLeft};
  header.Controls.Add(programHead);
  placers.Add(()=>programHead.Bounds=new Rectangle(Ui.Px(16),0,colX-Ui.Px(16),head));
  for(int i=0;i<Titles.Length;i++) {
   int index=i;
   var title=new Label{Text=Titles[i],Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Surface,Tag="muted",TextAlign=i<EdgeCount?ContentAlignment.MiddleLeft:ContentAlignment.MiddleCenter};
   header.Controls.Add(title);
   placers.Add(()=>title.Bounds=index<EdgeCount?new Rectangle(colLeft[index]+Ui.Px(8),0,colW[index]-Ui.Px(8),head):new Rectangle(colLeft[index],0,colW[index],head));
  }
  rows=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Ui.Surface};
  rows.HandleCreated+=(s,e)=>Native.DarkScroll(rows);
  for(int r=0;r<programs.Count;r++) {
   var entry=programs[r];
   int top=r*rowH;
   int lineTop=top+Ui.Px(6),lineH=Ui.Px(32);
   var name=new Label{Text=entry.Name+(entry.Custom?"  (Eigenes)":""),Font=Ui.UiSemibold,ForeColor=Ui.TextColor,BackColor=Ui.Surface,AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft};
   rows.Controls.Add(name);
   placers.Add(()=>name.Bounds=new Rectangle(Ui.Px(16),lineTop,colX-Ui.Px(20),lineH));
   var row=new CheckCell[Directions.Length];
   var fields=new TextBox[EdgeCount];
   var labels=new Label[EdgeCount];
   var chosen=new string[EdgeCount];
   Dictionary<string,string> entryRefs;
   if(!currentRefs.TryGetValue(entry.Id,out entryRefs))entryRefs=new Dictionary<string,string>();
   HashSet<string> allowed;
   if(!current.TryGetValue(entry.Id,out allowed))allowed=MainForm.DefaultEdgesFor(entry.Id);
   Dictionary<string,int> gaps;
   if(!currentGaps.TryGetValue(entry.Id,out gaps))gaps=new Dictionary<string,int>();
   for(int i=0;i<Directions.Length;i++) {
    bool edge=i<EdgeCount;
    int index=i;
    var cell=new CheckCell{Checked=allowed.Contains(Directions[i])};
    row[i]=cell;
    rows.Controls.Add(cell);
    placers.Add(()=>cell.Bounds=edge?new Rectangle(colLeft[index],lineTop,Ui.Px(36),lineH):new Rectangle(colLeft[index],lineTop,colW[index],lineH));
    if(!edge)continue;
    // Abstandsfeld erscheint nur bei angehakter Kante.
    var field=new Card{Size=new Size(Ui.Px(58),Ui.Px(30)),Padding=new Padding(Ui.Px(6),Ui.Px(6),Ui.Px(6),0)};
    placers.Add(()=>field.Location=new Point(colLeft[index]+Ui.Px(38),lineTop+(lineH-Ui.Px(30))/2));
    var box=new TextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,TextAlign=HorizontalAlignment.Center,MaxLength=4,Font=Ui.UiFont,BackColor=Ui.Surface,ForeColor=Ui.TextColor};
    if(gaps.ContainsKey(Directions[i]))box.Text=gaps[Directions[i]].ToString();
    string cue=globalGap.ToString();
    box.HandleCreated+=(s,e)=>SendMessage(((TextBox)s).Handle,0x1501,new IntPtr(1),cue);
    box.KeyPress+=(s,e)=>{if(!Char.IsDigit(e.KeyChar)&&!Char.IsControl(e.KeyChar))e.Handled=true;};
    field.Controls.Add(box);
    // Bezug unter dem Feld: "am Rand" oder "an <Programm>"; Klick öffnet die Auswahl.
    string refId;
    if(entryRefs.TryGetValue(Directions[i],out refId)&&programs.Any(x=>String.Equals(x.Id,refId,StringComparison.OrdinalIgnoreCase)&&x.Id!=entry.Id))chosen[i]=refId;
    var refLabel=new Label{Font=Ui.SmallFont,ForeColor=Ui.Accent,BackColor=Ui.Surface,Tag="accent",AutoEllipsis=true,UseMnemonic=false,Cursor=Cursors.Hand,TextAlign=ContentAlignment.MiddleLeft};
    placers.Add(()=>refLabel.Bounds=new Rectangle(colLeft[index]+Ui.Px(4),lineTop+lineH,colW[index]-Ui.Px(8),Ui.Px(20)));
    var owner=entry;
    refLabel.Click+=(s,e)=>ShowRefMenu(owner,index,(Label)s);
    field.Visible=refLabel.Visible=cell.Checked;
    cell.CheckedChanged+=(s,e)=>{field.Visible=cell.Checked;refLabel.Visible=cell.Checked;};
    rows.Controls.Add(field);rows.Controls.Add(refLabel);
    fields[i]=box;labels[i]=refLabel;
   }
   var measure=new ModernButton("Messen",ButtonKind.Ghost,null);
   measure.Height=Ui.Px(32);measure.Margin=new Padding(0);
   placers.Add(()=>measure.Location=new Point(colX+colW.Sum()+(measureW-measure.Width)/2,lineTop+(lineH-measure.Height)/2));
   var measured=entry;
   measure.Click+=(s,e)=>Measure(measured);
   rows.Controls.Add(measure);
   cells[entry.Id]=row;boxes[entry.Id]=fields;refs[entry.Id]=chosen;refLabels[entry.Id]=labels;
   for(int i=0;i<EdgeCount;i++)UpdateRefLabel(entry.Id,i);
  }
  card.Controls.Add(rows);card.Controls.Add(header);rows.BringToFront();
  Controls.Add(card);
  status=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.Muted,BackColor=Ui.Bg,Tag="muted",AutoEllipsis=true,UseMnemonic=false};
  Controls.Add(status);
  standard=new ModernButton("Standard",ButtonKind.Secondary,"");
  standard.Click+=(s,e)=>ResetToDefaults();
  ok=new ModernButton("Speichern",ButtonKind.Primary,null);
  cancel=new ModernButton("Abbrechen",ButtonKind.Secondary,null);
  cancel.DialogResult=DialogResult.Cancel;
  ok.Click+=(s,e)=>Accept();
  // Rote Fehlerzeile links neben den Knöpfen auf gleicher Höhe.
  error=new Label{Text="",Font=Ui.SmallFont,ForeColor=Ui.ErrorColor,BackColor=Ui.Bg,Tag="error",AutoEllipsis=true,UseMnemonic=false,TextAlign=ContentAlignment.MiddleRight};
  Controls.Add(standard);Controls.Add(error);Controls.Add(cancel);Controls.Add(ok);
  AcceptButton=ok;CancelButton=cancel;
  // Starthöhe: Beschreibung, Tabelle, Statuszeile und Knöpfe; im Hauptfenster füllt die Seite den Inhaltsbereich.
  ClientSize=new Size(w,Ui.Px(4)+DescriptionHeight(inner)+Ui.Px(6)+card.Height+Ui.Px(10)+Ui.Px(40)+Ui.Px(6)+Ui.Px(38)+Ui.Px(20));
  Resize+=(s,e)=>Arrange();
  Arrange();
 }
 int DescriptionHeight(int width) {
  return TextRenderer.MeasureText(description.Text,description.Font,new Size(width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+Ui.Px(4);
 }
 // Breite und Höhe verteilen: Die Spalten wachsen mit (bis 1,5-fach), der Rest geht an die Programmspalte;
 // die Tabelle nimmt die Höhe zwischen Beschreibung und Knöpfen ein und scrollt bei Bedarf.
 void Arrange() {
  int pad=Ui.Px(24),inner=Math.Max(Ui.Px(400),ClientSize.Width-2*pad);
  int y=Ui.Px(4);
  int descriptionHeight=DescriptionHeight(inner);
  description.Bounds=new Rectangle(pad,y,inner,descriptionHeight);
  y+=descriptionHeight+Ui.Px(6);
  int buttonsTop=ClientSize.Height-Ui.Px(20)-Ui.Px(38);
  int statusTop=buttonsTop-Ui.Px(6)-Ui.Px(40);
  int cardHeight=Math.Max(head+rowH+Ui.Px(10),statusTop-Ui.Px(10)-y);
  card.Bounds=new Rectangle(pad,y,inner,cardHeight);
  status.Bounds=new Rectangle(pad,card.Bottom+Ui.Px(10),inner,Ui.Px(40));
  buttonsTop=status.Bottom+Ui.Px(6);
  standard.Location=new Point(pad,buttonsTop);
  ok.Location=new Point(pad+inner-ok.Width,buttonsTop);
  // Globale Richtlinien, Einstellungsseiten: "Abbrechen" links, "Speichern" als Primärknopf ganz rechts, 10 Abstand.
  cancel.Location=new Point(ok.Left-Ui.Px(10)-cancel.Width,buttonsTop);
  error.Bounds=new Rectangle(standard.Right+Ui.Px(12),buttonsTop,Math.Max(0,cancel.Left-Ui.Px(12)-standard.Right-Ui.Px(12)),ok.Height);
  // Spalten: Grundbreiten 124 (Kanten), 70 (Mitte), 96 (Messen); Programmspalte mindestens 220.
  float baseSum=4*124+2*70+96;
  int available=inner-Ui.Px(14)-Ui.Px(220);
  float factor=Math.Max(1f,Math.Min(1.5f,available/(baseSum*Ui.S)));
  for(int i=0;i<colW.Length;i++)colW[i]=(int)Math.Round((i<EdgeCount?124:70)*factor*Ui.S);
  measureW=(int)Math.Round(96*factor*Ui.S);
  // Platz für die senkrechte Bildlaufleiste der Tabelle freihalten.
  int tableWidth=inner-Ui.Px(2)-SystemInformation.VerticalScrollBarWidth;
  colX=tableWidth-Ui.Px(12)-colW.Sum()-measureW;
  for(int i=0,x=colX;i<colW.Length;x+=colW[i],i++)colLeft[i]=x;
  // In einer gescrollten Tabelle gelten Positionen relativ zur Ansicht: erst nach oben, dann zurück.
  int scroll=-rows.AutoScrollPosition.Y;
  rows.AutoScrollPosition=new Point(0,0);
  rows.SuspendLayout();header.SuspendLayout();
  foreach(var place in placers)place();
  header.ResumeLayout();rows.ResumeLayout();
  rows.AutoScrollPosition=new Point(0,scroll);
 }
 Entry NeighborOf(string id){return id==null?null:programs.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));}
 void UpdateRefLabel(string id,int index) {
  var neighbor=NeighborOf(refs[id][index]);
  refLabels[id][index].Text=neighbor==null?"am Rand ▾":"an "+neighbor.Name+" ▾";
 }
 // Auswahl, wovon der Abstand gilt: Bildschirmrand oder ein anderes Programm der Liste.
 void ShowRefMenu(Entry entry,int index,Label anchor) {
  MenuStyle.Prepare(refMenu);
  refMenu.Items.Add(MenuStyle.TopSpacer());
  string current=refs[entry.Id][index];
  refMenu.Items.Add(MenuStyle.Item("Bildschirmrand",current==null?'\uE73E':'\0',(s,a)=>{refs[entry.Id][index]=null;UpdateRefLabel(entry.Id,index);}));
  refMenu.Items.Add(new ToolStripSeparator());
  string side=Directions[index]=="left"?"rechts neben":Directions[index]=="right"?"links neben":Directions[index]=="top"?"unter":"über";
  foreach(var other in programs) {
   if(other.Id==entry.Id)continue;
   var target=other;
   bool isCurrent=String.Equals(current,other.Id,StringComparison.OrdinalIgnoreCase);
   refMenu.Items.Add(MenuStyle.Item(side+" "+other.Name,isCurrent?'\uE73E':'\0',(s,a)=>{refs[entry.Id][index]=target.Id;UpdateRefLabel(entry.Id,index);}));
  }
  refMenu.Items.Add(MenuStyle.BottomSpacer());
  refMenu.Show(anchor,new Point(0,anchor.Height));
 }
 // Aktuelle Abstände des offenen Fensters in die Felder der angehakten Kanten übernehmen.
 void Measure(Entry entry) {
  var row=cells[entry.Id];var fields=boxes[entry.Id];var chosen=refs[entry.Id];
  Dictionary<string,int> m;
  Rectangle own;
  try {m=VisibleAlignment.MeasureGaps(entry);}
  catch(Exception ex){status.Text=ex.Message;return;}
  VisibleAlignment.TryVisibleRect(entry,out own);
  var taken=new List<string>();
  for(int i=0;i<EdgeCount;i++) {
   if(!row[i].Checked)continue;
   int measured=m[Directions[i]];
   string suffix="";
   Entry neighbor=NeighborOf(chosen[i]);
   if(neighbor!=null) {
    // Abstand zur gegenüberliegenden Kante des Nachbarprogramms.
    Rectangle r;
    if(!VisibleAlignment.TryVisibleRect(neighbor,out r)){taken.Add(Titles[i]+": "+neighbor.Name+" ist nicht offen");continue;}
    measured=Directions[i]=="left"?own.Left-r.Right:Directions[i]=="right"?r.Left-own.Right:Directions[i]=="top"?own.Top-r.Bottom:r.Top-own.Bottom;
    suffix=" bis "+neighbor.Name;
    if(measured<0)suffix+=" (überlappt, 0 gesetzt)";
   }
   int value=Math.Max(0,Math.Min(MainForm.MaxEdgeGap,measured));
   fields[i].Text=value.ToString();
   taken.Add(Titles[i]+" "+value+" px"+suffix);
  }
  string all="oben "+m["top"]+", unten "+m["bottom"]+", links "+m["left"]+", rechts "+m["right"]+" px";
  status.Text=taken.Count>0?entry.Name+" gemessen ("+all+" zum Rand) – übernommen: "+String.Join(", ",taken.ToArray())+".":entry.Name+" gemessen ("+all+"). Keine Kante angehakt – zuerst die Kanten anhaken, die übernommen werden sollen.";
 }
 void ResetToDefaults() {
  foreach(var entry in programs) {
   var allowed=MainForm.DefaultEdgesFor(entry.Id);
   var row=cells[entry.Id];
   for(int i=0;i<Directions.Length;i++)row[i].Checked=allowed.Contains(Directions[i]);
   foreach(var box in boxes[entry.Id])box.Text="";
   for(int i=0;i<EdgeCount;i++){refs[entry.Id][i]=null;UpdateRefLabel(entry.Id,i);}
  }
  status.Text="Vorgaben wiederhergestellt (erst mit „Speichern“ übernommen).";
 }
 void Accept() {
  error.Text="";
  var rules=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  var allGaps=new Dictionary<string,Dictionary<string,int>>(StringComparer.OrdinalIgnoreCase);
  var allRefs=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in programs) {
   var set=new HashSet<string>();
   var gaps=new Dictionary<string,int>();
   var row=cells[entry.Id];var fields=boxes[entry.Id];
   for(int i=0;i<Directions.Length;i++) {
    if(!row[i].Checked)continue;
    set.Add(Directions[i]);
    if(i>=EdgeCount)continue;
    string text=fields[i].Text.Trim();
    if(text.Length==0)continue;
    int value;
    // Erst alles prüfen, dann speichern.
    if(!Int32.TryParse(text,out value)||value<0||value>MainForm.MaxEdgeGap) {
     error.Text=entry.Name+", "+Titles[i]+": Abstand 0 bis "+MainForm.MaxEdgeGap+" px";
     fields[i].Focus();fields[i].SelectAll();
     return;
    }
    gaps[Directions[i]]=value;
   }
   var entryRefs=new Dictionary<string,string>();
   for(int i=0;i<EdgeCount;i++)if(row[i].Checked&&refs[entry.Id][i]!=null)entryRefs[Directions[i]]=refs[entry.Id][i];
   rules[entry.Id]=set;allGaps[entry.Id]=gaps;allRefs[entry.Id]=entryRefs;
  }
  Rules=rules;Gaps=allGaps;Refs=allRefs;
  if(Save!=null) {
   string message=Save();
   if(message!=null){error.Text=message;return;}
  }
  DialogResult=DialogResult.OK;
  Close();
 }
}
// Einzelnes Häkchen (wie in der Programmliste), anklickbar und per Leertaste umschaltbar.
public class CheckCell:Control {
 bool isChecked;
 public CheckCell() {
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
  BackColor=Ui.Surface;Cursor=Cursors.Hand;TabStop=true;
 }
 public event EventHandler CheckedChanged;
 public bool Checked{get{return isChecked;}set{if(isChecked==value)return;isChecked=value;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button==MouseButtons.Left){Focus();Checked=!Checked;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;}}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;
  g.Clear(BackColor);
  g.SmoothingMode=SmoothingMode.AntiAlias;
  int size=Ui.Px(20);
  var box=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
  ProgramList.DrawCheck(g,box,isChecked,Enabled);
  if(Focused&&ShowFocusCues)
   using(var path=Ui.Round(Rectangle.Inflate(box,Ui.Px(3),Ui.Px(3)),Ui.Px(5)))
   using(var pen=new Pen(Ui.Blend(Ui.Accent,Ui.Surface,0.3f))) g.DrawPath(pen,path);
 }
}
}
