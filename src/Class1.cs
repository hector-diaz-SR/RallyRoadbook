using SimHub.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using GameReaderCommon;

namespace Hector.RallyNoteManager
{
    [PluginDescription("Gestor Nativo de Notas de Rally v8.3 - Final")]
    [PluginName("RallyPlugin")]
    public class RallyPlugin : IPlugin, IDataPlugin
    {
        public PluginManager PluginManager { get; set; }
        public System.Windows.Media.ImageSource ImageSource => null;

        private int side = 0, intensity = 6, modifier = 0;
        private string status = "READY v8.3";
        private List<RallyNote> tempNotes = new List<RallyNote>();
        private Dictionary<string, int> lastStates = new Dictionary<string, int>();

        public void Init(PluginManager pluginManager)
        {
            this.PluginManager = pluginManager;
            pluginManager.AddProperty("side", this.GetType(), 0);
            pluginManager.AddProperty("intensity", this.GetType(), 6);
            pluginManager.AddProperty("modifier", this.GetType(), 0);
            pluginManager.AddProperty("status", this.GetType(), status);
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            string[] triggers = { "Side_Up", "Side_Down", "Int_Up", "Int_Down", "Mod_Up", "Mod_Down", "Rec_Point", "Rec_Save" };

            foreach (var t in triggers)
            {
                var rawValue = pluginManager.GetPropertyValue("InputStatus.GraphicalDashPlugin." + t);
                if (rawValue == null) continue;

                int state = Convert.ToInt32(rawValue);
                if (state == 1 && (!lastStates.ContainsKey(t) || lastStates[t] == 0))
                {
                    if (t == "Side_Up") side = (side + 1) % 3;
                    if (t == "Side_Down") side = (side == 0) ? 2 : side - 1;
                    if (t == "Int_Up") intensity = (intensity % 6) + 1;
                    if (t == "Int_Down") intensity = (intensity == 1) ? 6 : intensity - 1;
                    if (t == "Mod_Up" || t == "Mod_Down") modifier = (modifier + 1) % 2;

                    if (t == "Rec_Point")
                    {
                        double pct = (data.NewData != null) ? data.NewData.TrackPositionPercent : 0;
                        tempNotes.Add(new RallyNote
                        {
                            id = tempNotes.Count + 1,
                            posPct = pct,
                            side = new[] { "warning", "left", "right" }[side],
                            intensity = (side == 0) ? 0 : intensity,
                            modifier = (side == 0) ? "alert" : new[] { "normal", "nocut" }[modifier]
                        });
                        status = "POINT " + tempNotes.Count + " LOGGED!";
                    }

                    if (t == "Rec_Save")
                    {
                        string trk = (data.NewData != null && !string.IsNullOrEmpty(data.NewData.TrackName)) ? data.NewData.TrackName : "Manual_Save";
                        SaveNotes(trk);
                    }
                }
                lastStates[t] = state;
            }

            pluginManager.SetPropertyValue("side", this.GetType(), side);
            pluginManager.SetPropertyValue("intensity", this.GetType(), intensity);
            pluginManager.SetPropertyValue("modifier", this.GetType(), modifier);
            pluginManager.SetPropertyValue("status", this.GetType(), status);
        }

        private void SaveNotes(string track)
        {
            try
            {
                if (tempNotes.Count == 0) { status = "ERR: NO POINTS"; return; }

                string trackClean = string.Concat(track.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Replace(" ", "_");
                string folder = @"D:\SimHub\DashTemplates\Rally_Notes_v7\";

                // SEGURIDAD: Creamos la carpeta si no existe
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, trackClean + ".json");
                var export = new { track = track, notes = tempNotes.OrderBy(n => n.posPct).ToList() };

                // ESCRIBIMOS Y ACTUALIZAMOS STATUS
                File.WriteAllText(path, JsonConvert.SerializeObject(export, Formatting.Indented));
                status = "SAVED: " + trackClean + ".json";

                // VACIAR MEMORIA (Opcional, para no duplicar en el siguiente guardado)
                tempNotes.Clear();
            }
            catch (Exception e)
            {
                status = "DISK ERR: " + e.Message.Substring(0, Math.Min(15, e.Message.Length));
            }
        }

        public void End(PluginManager pluginManager) { }
    }

    public class RallyNote
    {
        public int id { get; set; }
        public double posPct { get; set; }
        public string side { get; set; }
        public int intensity { get; set; }
        public string modifier { get; set; }
    }
}
