using System;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;

namespace AcerFanControl {

	internal class TrayIcon : ApplicationContext {
		private Configuration Config => Program.Config;

		private NotifyIcon notifyIcon;
		private IContainer components;
		private ContextMenu contextMenu;

		internal MenuItem[] StandardMenuItems;
		internal ProfileMenuItem MenuItem_BIOS;
		internal MenuItem MenuItem_ConfigFile;
		internal MenuItem MenuItem_Exit;

		public TrayIcon() {
			components = new System.ComponentModel.Container();
			notifyIcon = new NotifyIcon(this.components);
			RenderIcon(255, 0);
			notifyIcon.Visible = true;
			contextMenu = new ContextMenu();
			notifyIcon.ContextMenu = contextMenu;

			MenuItem_BIOS = new ProfileMenuItem(new FanProfile() { Name = "BIOS", IsBIOS = true, Interval=Config.General.interval });
			MenuItem_BIOS.Click += HandleProfileMenuItemClick;

			MenuItem_ConfigFile = new MenuItem("Create " + Configuration.DefaultConfigFileName);
			MenuItem_ConfigFile.Click += HandleCreateConfigFileEvent;

			MenuItem_Exit = new MenuItem("Exit");
			MenuItem_Exit.Click += (sender, e) => { notifyIcon.Visible = false; Application.Exit(); };

			StandardMenuItems = new [] {	new MenuItem("-"),
													MenuItem_BIOS,
													new MenuItem("-"),
													MenuItem_ConfigFile,
													new MenuItem("-"),
													MenuItem_Exit
			};

		}

		private void HandleCreateConfigFileEvent(Object sender, EventArgs ev) => SaveOrLoadConfigFile(false);
		private void HandleLoadConfigFileEvent(Object sender, EventArgs ev) => SaveOrLoadConfigFile(true);

		private async void SaveOrLoadConfigFile(bool bLoad) {
			MenuItem micf = MenuItem_ConfigFile;
			micf.Click -= HandleCreateConfigFileEvent; micf.Click -= HandleLoadConfigFileEvent;
			try {
				if (bLoad) {
					await Config.LoadDefaultConfigFile();
					RebuildMenuItems();
				} else {
					await Config.SaveDefaultConfigFile();
				}
				micf.Text = "Reload " + Configuration.DefaultConfigFileName;
				micf.Click += HandleLoadConfigFileEvent;

			} catch (Exception ex) {
				micf.Text = "Create New " + Configuration.DefaultConfigFileName;
				micf.Click += HandleCreateConfigFileEvent;
				string msg = "Failed to " + (bLoad ? "load " : "save ") + Configuration.DefaultConfigFileName + "\n\n" + ex.ToString();
				MessageBox.Show(msg, Program.ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		public void Init() {
			Exception ex = Program.FanController.CheckTvicPortAvailability();
			if (ex != null) {
				string msg = $@"TVicPort failed or is not installed. Please find and install 'TVicPortInstall41.exe'. 
There seems to be a lot of sketchy sites offering the download. The file should have an MD5 hash of: FAF4A7329BE416530E47D9CB16D418E5
\n Full Error Text:
{ex.ToString()}";
				MessageBox.Show(msg, Program.ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
				Application.Exit();
			}

			RebuildMenuItems();

			if (File.Exists(Configuration.DefaultConfigFileName)) {
				SaveOrLoadConfigFile(true);
			}
		}

		public void RebuildMenuItems() {
			contextMenu.MenuItems.Clear();

			FanProfile selected = null;

			for (int i = 0, len = Config.AllProfiles.Length; i < len; i++) {
				FanProfile profile = Config.AllProfiles[i];
				ProfileMenuItem menuItem = new ProfileMenuItem(profile);
				menuItem.Click += HandleProfileMenuItemClick;
				contextMenu.MenuItems.Add(menuItem);
				if (profile.IsDefault && selected == null) {
					menuItem.Checked = true;
					selected = profile;
				}
			}

			for (int i = 0, len = StandardMenuItems.Length; i < len; i++) {
				contextMenu.MenuItems.Add(StandardMenuItems[i]);
			}

			Program.Regulator.RunProfile(selected ?? MenuItem_BIOS.Profile);
		}

		private void HandleProfileMenuItemClick(Object sender, EventArgs ev) {
			for (int i = 0, len = contextMenu.MenuItems.Count; i < len; i++) {
				if (contextMenu.MenuItems[ i ] is ProfileMenuItem item) { item.Checked = false; }
			}
			ProfileMenuItem pmi = (ProfileMenuItem)sender;
			pmi.Checked = true;
			Program.Regulator.RunProfile(pmi.Profile);
		}

		
		private byte prevTemp = 0;
		private byte prevSpeed = 0;
		private FanProfile activeProfile = null;
		private StringBuilder _sbIcon = new StringBuilder(32);

		private void HandleMouseOverIcon(Object sender, EventArgs ev) {

		}

		public void Update(FanProfile profile, byte temp, byte speed) {
			if(activeProfile != profile) {
				for(int i = 0, len = contextMenu.MenuItems.Count; i<len; i++) {
					if (contextMenu.MenuItems[ i ] is ProfileMenuItem item) { item.Checked = (profile.MenuItem == item); }
				}
			}
			if(prevTemp != temp || prevSpeed != speed || activeProfile != profile) {
				activeProfile = profile;
				prevTemp = temp;
				prevSpeed = speed;
				RenderIcon(temp, speed);
				_sbIcon.Append(profile == null ? "BIOS" : profile.Name).AppendLine();
				_sbIcon.Append("CPU:").Append(temp).Append(", Fan:").Append(speed).Append('%');
				notifyIcon.Text = _sbIcon.ToString();
				_sbIcon.Clear();
			}
		}


		[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)] extern static bool DestroyIcon(IntPtr handle);

		private Bitmap _trayIcon16 = Properties.Resources.icon16;
		private Bitmap _trayFont16 = Properties.Resources.font16;
		private Brush _whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255));

		private void RenderIcon(byte temp, byte fan) {
			if(temp > 100) { temp = 100; }
			if(fan > 100) { fan = 100; }
			using (Bitmap bitmap = new Bitmap(_trayIcon16))
			using (Graphics graphics = Graphics.FromImage(bitmap)) {
				if (temp <= 99) {
					graphics.DrawImage(_trayFont16, new Rectangle(0, 0, 8, 11), new Rectangle(((temp / 10) % 10) * 8, 0, 8, 11), GraphicsUnit.Pixel);
					graphics.DrawImage(_trayFont16, new Rectangle(8, 0, 8, 11), new Rectangle((temp % 10) * 8, 0, 8, 11), GraphicsUnit.Pixel);
				} else {
					graphics.DrawImage(_trayFont16, new Rectangle(0, 0, 8, 11), new Rectangle(80, 0, 8, 11), GraphicsUnit.Pixel);
					graphics.DrawImage(_trayFont16, new Rectangle(8, 0, 8, 11), new Rectangle(80, 0, 8, 11), GraphicsUnit.Pixel);
				}
				graphics.FillRectangle(_whiteBrush, 1, 12, fan * 14 / 100, 3);
				Icon prev = notifyIcon.Icon;
				notifyIcon.Icon = Icon.FromHandle(bitmap.GetHicon());
				if (prev != null) { DestroyIcon(prev.Handle);  }			
			}			
		}


		internal class ProfileMenuItem : MenuItem {
			internal FanProfile Profile { get; }
			public ProfileMenuItem(FanProfile profile) : base(profile.Name) {
				base.Name = profile.Name;
				Profile = profile;
				profile.MenuItem = this;
			}
			
		}
	}
}
