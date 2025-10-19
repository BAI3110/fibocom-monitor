namespace FibocomMonitor
{
    partial class MainWindow
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Operator = new Label();
            LB1 = new Label();
            LB2 = new Label();
            RSRP = new Label();
            LB4 = new Label();
            Band = new Label();
            LB5 = new Label();
            CarrierList = new ListBox();
            buttonCon = new Button();
            COMList = new ComboBox();
            PortClose = new Button();
            LB6 = new Label();
            StatusNetwork = new Label();
            FCN = new Label();
            LB9 = new Label();
            SINR = new Label();
            LB11 = new Label();
            label1 = new Label();
            label3 = new Label();
            RSSI = new Label();
            label2 = new Label();
            Signal = new Label();
            label4 = new Label();
            Distance = new Label();
            SuspendLayout();
            // 
            // Operator
            // 
            Operator.AutoSize = true;
            Operator.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            Operator.Location = new Point(90, 49);
            Operator.Name = "Operator";
            Operator.Size = new Size(82, 23);
            Operator.TabIndex = 0;
            Operator.Text = "Unknown";
            // 
            // LB1
            // 
            LB1.AutoSize = true;
            LB1.Location = new Point(11, 52);
            LB1.Name = "LB1";
            LB1.Size = new Size(72, 20);
            LB1.TabIndex = 1;
            LB1.Text = "Operator:";
            // 
            // LB2
            // 
            LB2.AutoSize = true;
            LB2.Location = new Point(212, 76);
            LB2.Name = "LB2";
            LB2.Size = new Size(46, 20);
            LB2.TabIndex = 2;
            LB2.Text = "RSRP:";
            // 
            // RSRP
            // 
            RSRP.AutoSize = true;
            RSRP.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            RSRP.Location = new Point(259, 73);
            RSRP.Name = "RSRP";
            RSRP.Size = new Size(82, 23);
            RSRP.TabIndex = 3;
            RSRP.Text = "Unknown";
            // 
            // LB4
            // 
            LB4.AutoSize = true;
            LB4.Location = new Point(11, 232);
            LB4.Name = "LB4";
            LB4.Size = new Size(46, 20);
            LB4.TabIndex = 6;
            LB4.Text = "Band:";
            // 
            // Band
            // 
            Band.AutoSize = true;
            Band.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            Band.Location = new Point(83, 229);
            Band.Name = "Band";
            Band.Size = new Size(92, 23);
            Band.TabIndex = 7;
            Band.Text = "Unknownn";
            // 
            // LB5
            // 
            LB5.AutoSize = true;
            LB5.Location = new Point(11, 201);
            LB5.Name = "LB5";
            LB5.Size = new Size(66, 20);
            LB5.TabIndex = 8;
            LB5.Text = "EARFCN:";
            // 
            // CarrierList
            // 
            CarrierList.FormattingEnabled = true;
            CarrierList.Location = new Point(12, 297);
            CarrierList.Name = "CarrierList";
            CarrierList.Size = new Size(890, 244);
            CarrierList.TabIndex = 9;
            // 
            // buttonCon
            // 
            buttonCon.Location = new Point(169, 7);
            buttonCon.Name = "buttonCon";
            buttonCon.Size = new Size(94, 29);
            buttonCon.TabIndex = 11;
            buttonCon.Text = "Open port";
            buttonCon.UseVisualStyleBackColor = true;
            buttonCon.Click += ButtonCon_Click;
            // 
            // COMList
            // 
            COMList.FormattingEnabled = true;
            COMList.Location = new Point(12, 8);
            COMList.Name = "COMList";
            COMList.Size = new Size(151, 28);
            COMList.TabIndex = 12;
            // 
            // PortClose
            // 
            PortClose.Location = new Point(269, 7);
            PortClose.Name = "PortClose";
            PortClose.Size = new Size(94, 29);
            PortClose.TabIndex = 13;
            PortClose.Text = "Close port";
            PortClose.UseVisualStyleBackColor = true;
            PortClose.Click += PortClose_Click;
            // 
            // LB6
            // 
            LB6.AutoSize = true;
            LB6.Location = new Point(11, 76);
            LB6.Name = "LB6";
            LB6.Size = new Size(43, 20);
            LB6.TabIndex = 14;
            LB6.Text = "Type:";
            // 
            // StatusNetwork
            // 
            StatusNetwork.AutoSize = true;
            StatusNetwork.Font = new Font("Segoe UI", 10.8F, FontStyle.Regular, GraphicsUnit.Point, 204);
            StatusNetwork.Location = new Point(90, 71);
            StatusNetwork.Name = "StatusNetwork";
            StatusNetwork.Size = new Size(87, 25);
            StatusNetwork.TabIndex = 15;
            StatusNetwork.Text = "Unknown";
            // 
            // FCN
            // 
            FCN.AutoSize = true;
            FCN.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            FCN.Location = new Point(83, 198);
            FCN.Name = "FCN";
            FCN.Size = new Size(82, 23);
            FCN.TabIndex = 16;
            FCN.Text = "Unknown";
            // 
            // LB9
            // 
            LB9.AutoSize = true;
            LB9.Location = new Point(212, 104);
            LB9.Name = "LB9";
            LB9.Size = new Size(44, 20);
            LB9.TabIndex = 19;
            LB9.Text = "SINR:";
            // 
            // SINR
            // 
            SINR.AutoSize = true;
            SINR.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            SINR.Location = new Point(259, 104);
            SINR.Name = "SINR";
            SINR.Size = new Size(82, 23);
            SINR.TabIndex = 20;
            SINR.Text = "Unknown";
            // 
            // LB11
            // 
            LB11.AutoSize = true;
            LB11.Location = new Point(11, 169);
            LB11.Name = "LB11";
            LB11.Size = new Size(116, 20);
            LB11.TabIndex = 21;
            LB11.Text = "Cell information";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(212, 49);
            label1.Name = "label1";
            label1.Size = new Size(72, 20);
            label1.TabIndex = 22;
            label1.Text = "Main cell:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(212, 137);
            label3.Name = "label3";
            label3.Size = new Size(41, 20);
            label3.TabIndex = 24;
            label3.Text = "RSSI:";
            // 
            // RSSI
            // 
            RSSI.AutoSize = true;
            RSSI.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 204);
            RSSI.Location = new Point(259, 135);
            RSSI.Name = "RSSI";
            RSSI.Size = new Size(82, 23);
            RSSI.TabIndex = 25;
            RSSI.Text = "Unknown";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(212, 169);
            label2.Name = "label2";
            label2.Size = new Size(53, 20);
            label2.TabIndex = 26;
            label2.Text = "Signal:";
            // 
            // Signal
            // 
            Signal.AutoSize = true;
            Signal.Location = new Point(271, 169);
            Signal.Name = "Signal";
            Signal.Size = new Size(70, 20);
            Signal.TabIndex = 27;
            Signal.Text = "Unknown";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(215, 201);
            label4.Name = "label4";
            label4.Size = new Size(69, 20);
            label4.TabIndex = 28;
            label4.Text = "Distance:";
            // 
            // Distance
            // 
            Distance.AutoSize = true;
            Distance.Location = new Point(290, 201);
            Distance.Name = "Distance";
            Distance.Size = new Size(70, 20);
            Distance.TabIndex = 29;
            Distance.Text = "Unknown";
            // 
            // MainWindow
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(914, 559);
            Controls.Add(Distance);
            Controls.Add(label4);
            Controls.Add(Signal);
            Controls.Add(label2);
            Controls.Add(RSSI);
            Controls.Add(label3);
            Controls.Add(label1);
            Controls.Add(LB11);
            Controls.Add(SINR);
            Controls.Add(LB9);
            Controls.Add(FCN);
            Controls.Add(StatusNetwork);
            Controls.Add(LB6);
            Controls.Add(PortClose);
            Controls.Add(COMList);
            Controls.Add(buttonCon);
            Controls.Add(CarrierList);
            Controls.Add(LB5);
            Controls.Add(Band);
            Controls.Add(LB4);
            Controls.Add(RSRP);
            Controls.Add(LB2);
            Controls.Add(LB1);
            Controls.Add(Operator);
            Name = "MainWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Fibocom monitor";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label Operator;
        private Label LB1;
        private Label LB2;
        private Label RSRP;
        private Label LB4;
        private Label Band;
        private Label LB5;
        private ListBox CarrierList;
        private Button buttonCon;
        private ComboBox COMList;
        private Button PortClose;
        private Label LB6;
        private Label StatusNetwork;
        private Label FCN;
        private Label LB9;
        private Label SINR;
        private Label LB11;
        private Label label1;
        private Label label3;
        private Label RSSI;
        private Label label2;
        private Label Signal;
        private Label label4;
        private Label Distance;
    }
}
