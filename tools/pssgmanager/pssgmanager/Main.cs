﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Windows.Forms;
using FreeImageAPI;

namespace PSSGManager {
	public partial class Main : Form {
		private CPSSGFile pssgFile;

		public Main() {
			InitializeComponent();
		}

		#region MainMenu
		private void openToolStripMenuItem_Click(object sender, EventArgs e) {
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.Filter = "PSSG files|*.pssg|All files|*.*";
			dialog.Title = "Select a PSSG file";
			if (dialog.ShowDialog() == DialogResult.OK) {
				closeFile();
				StreamReader sr = new StreamReader(dialog.FileName);
				CPSSGFile f = new CPSSGFile(sr.BaseStream);
				pssgFile = f;
				treeView.Nodes.Add(createTreeViewNode(f.rootNode));
		                createTreeViewTexturesList(f.rootNode);
			} else {

			}
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e) {
			closeFile();
		}

		private void closeFile() {
			if (this.pssgFile == null) return;

			// All tab
			treeView.Nodes.Clear();
			dataGridViewAttributes.Rows.Clear();

			// Models tab
			listBox1.Items.Clear();

			// Textures tab
			treeViewTextures.Nodes.Clear();
			pictureBoxTextures.Image = null;

			pssgFile = null;
		}
		#endregion

		#region All
		private TreeNode createTreeViewNode(CNode node) {
			TreeNode treeNode = new TreeNode();
			treeNode.Text = node.name;
			treeNode.Tag = node;
			if (node.subNodes != null) {
				foreach (CNode subNode in node.subNodes) {
					treeNode.Nodes.Add(createTreeViewNode(subNode));
				}
			}
			return treeNode;
		}

		private void treeView_AfterSelect(object sender, TreeViewEventArgs e) {
			if (treeView.SelectedNode != null) {
				dataGridViewAttributes.Rows.Clear();
				foreach (KeyValuePair<string, CAttribute> pair in ((CNode)treeView.SelectedNode.Tag).attributes) {
					dataGridViewAttributes.Rows.Add(pair.Key, pair.Value);
				}
			}
		}

		private void buttonExportAll_Click(object sender, EventArgs e) {
			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "XML files|*.xml|All files|*.*";
			dialog.Title = "Select location to save XML output";
			if (dialog.ShowDialog() == DialogResult.OK) {
				XmlWriterSettings settings = new XmlWriterSettings();
				settings.Indent = true;
				using (XmlWriter writer = XmlWriter.Create(dialog.FileName, settings)) {
					writer.WriteStartDocument();
					writeNodesToXml(writer, pssgFile.rootNode);
					writer.WriteEndDocument();
				}
			}
		}

		private void writeNodesToXml(XmlWriter writer, CNode node) {
			writer.WriteStartElement(node.name.ToLower());
			foreach (KeyValuePair<string, CAttribute> pair in node.attributes) {
				writer.WriteElementString(pair.Key, pair.Value.ToString());
			}
			if (node.subNodes != null) {
				foreach (CNode subNode in node.subNodes) {
					writeNodesToXml(writer, subNode);
				}
			}
			writer.WriteEndElement();
		}
		#endregion

		#region Textures
		private void createTreeViewTexturesList(CNode node) {
			if (node.name == "TEXTURE") {
				TreeNode treeNode = new TreeNode();
				treeNode.Text = node.attributes["id"].value;
				treeNode.Tag = node;
				treeViewTextures.Nodes.Add(treeNode);
			}
			if (node.subNodes != null) {
				foreach (CNode subNode in node.subNodes) {
					createTreeViewTexturesList(subNode);
				}
			}
		}

		private void treeViewTextures_AfterSelect(object sender, TreeViewEventArgs e) {
			createPreview();
		}
		private void createPreview() {
			int height = 0; int width = 0;
			pictureBoxTextures.Dock = DockStyle.Fill;
			height = pictureBoxTextures.Height;
			width = pictureBoxTextures.Width;
			writeDDS(Application.StartupPath + "\\temp.dds", null);
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_DDS;
			System.Drawing.Bitmap image = FreeImage.LoadBitmap(Application.StartupPath + "\\temp.dds", FREE_IMAGE_LOAD_FLAGS.DEFAULT, ref format);
			if (image.Height <= height && image.Width <= width) {
				pictureBoxTextures.Dock = DockStyle.None;
				pictureBoxTextures.Width = image.Width;
				pictureBoxTextures.Height = image.Height;
			}
			pictureBoxTextures.Image = image;
		}

		private void toolStripButtonExport_Click(object sender, EventArgs e) {
			if (treeViewTextures.Nodes.Count == 0 || treeViewTextures.SelectedNode.Index == -1)
			{
				return;
			}
			CNode node = ((CNode)treeViewTextures.SelectedNode.Tag);
			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "DDS files|*.dds|All files|*.*";
			dialog.Title = "Select the dds save location and file name";
			dialog.FileName = node.attributes["id"].value + ".dds";
			if (dialog.ShowDialog() == DialogResult.OK) {
				writeDDS(dialog.FileName, node);
			}
		}
		private void writeDDS(string ddsPath, CNode node) {
			byte[] ddh;
			if (node == null) {
				node = ((CNode)treeViewTextures.SelectedNode.Tag);
			}
			using (BinaryReader b = new BinaryReader(File.Open(Application.StartupPath + "\\dxt.ddh", FileMode.Open))) {
				ddh = b.ReadBytes(128);
			}
			using (BinaryWriter b = new BinaryWriter(File.Open(ddsPath, FileMode.Create))) {
				b.Write(ddh);
				b.Write(node.subNodes[0].subNodes[0].data);
			}
			using (Stream outStream = File.Open(ddsPath, FileMode.Open)) {
				outStream.Seek(12, SeekOrigin.Begin);
				// Change Height and Width
				outStream.Write(BitConverter.GetBytes((int)node.attributes["height"].data), 0, 4);
				outStream.Write(BitConverter.GetBytes((int)node.attributes["width"].data), 0, 4);
				// Change size
				switch ((string)node.attributes["texelFormat"].data) {
					case "dxt1":
						outStream.Write(BitConverter.GetBytes(((int)node.attributes["width"].data) * ((int)node.attributes["height"].data) / 2), 0, 4);
						break;
					default:
						outStream.Write(BitConverter.GetBytes(((int)node.attributes["width"].data) * ((int)node.attributes["height"].data)), 0, 4);
						break;
				}
				// Skip 4
				outStream.Seek(4, SeekOrigin.Current);
				// Change numberMipMapLevels + 1
				outStream.Write(BitConverter.GetBytes((int)node.attributes["numberMipMapLevels"].data + 1), 0, 4);
				// Skip 52
				outStream.Seek(52, SeekOrigin.Current);
				// Change Format
				outStream.Write(Encoding.UTF8.GetBytes(((string)node.attributes["texelFormat"].data).ToUpper()), 0, 4);
			}
		}

		private void toolStripButtonImport_Click(object sender, EventArgs e) {
			if (treeViewTextures.Nodes.Count == 0 || treeViewTextures.SelectedNode.Index == -1) {
				return;
			}
			CNode node = ((CNode)treeViewTextures.SelectedNode.Tag);
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.Filter = "DDS files|*.dds|All files|*.*";
			dialog.Title = "Select a dds file";
			dialog.FileName = node.attributes["id"].value + ".dds";
			if (dialog.ShowDialog() == DialogResult.OK) {
				readDDS(dialog.FileName, node);
				createPreview();
			}
		}
		private void readDDS(string ddsPath, CNode node) {
			if (node == null) {
				node = node = ((CNode)treeViewTextures.SelectedNode.Tag);
			}
			int nMML = 0;
			using (BinaryReader b = new BinaryReader(File.Open(ddsPath, FileMode.Open))) {
				// Skip first 12 bytes
				b.ReadBytes(12);
				// Height
				node.attributes["height"].data = b.ReadInt32();
				// Width
				node.attributes["width"].data = b.ReadInt32();
				// Skip 8
				b.ReadBytes(8);
				// numberMipMapLevels
				nMML = b.ReadInt32() - 1;
				if (nMML >= 0) {
					node.attributes["numberMipMapLevels"].data = nMML;
				} else {
					node.attributes["numberMipMapLevels"].data = 0;
				}
				// Skip 52
				b.ReadBytes(52);
				// texelFormat
				node.attributes["texelFormat"].data = Encoding.UTF8.GetString(b.ReadBytes(4)).ToLower();
				// Skip 40
				b.ReadBytes(40);
				// Code
				node.subNodes[0].subNodes[0].data = b.ReadBytes((int)(b.BaseStream.Length - (long)128));
				// TEXTUREIMAGEBLOCKDATA-Size
				//sections[sectionIndex + 2].Size = sections[sectionIndex + 2].Code.Length + 4;
				// TEXTUREIMAGEBLOCK_size
				node.subNodes[0].attributes["size"].data = node.subNodes[0].subNodes[0].data.Length;
				// TEXTUREIMAGEBLOCK-Size
				//sections[sectionIndex + 1].Size = bytesToInt(sections[sectionIndex + 1].Subsections["size"].Value) + sections[sectionIndex + 1].Size2 + 16;
			}
		}
		#endregion
	}
}
