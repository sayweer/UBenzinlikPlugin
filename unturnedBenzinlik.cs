using Rocket.API;
using Rocket.Core.Commands;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class LTBenzinlik : RocketPlugin<LTBenzinlikConfig>
{
    public static LTBenzinlik Instance;
    private List<FuelStation> fuelStations = new List<FuelStation>();
    private bool isRunning = true;
    private Dictionary<CSteamID, DateTime> exitCooldowns = new Dictionary<CSteamID, DateTime>();
    private Dictionary<CSteamID, DateTime> effectCooldowns = new Dictionary<CSteamID, DateTime>();
    private string searchedUnturnedPlayer = "";

    protected override void Load()
    {
        Instance = this;
        fuelStations = Configuration.Instance.FuelStations;
        Rocket.Core.Logging.Logger.Log("LTBenzinlik plugini aktif.");
        Rocket.Core.Logging.Logger.Log("Satın alım için teşekkürler.");
        EffectManager.onEffectButtonClicked += OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted += TextCommitted;
        StartPositionCheckLoop();
        StartParaBiriktirmeLoop();
    }

    protected override void Unload()
    {
        EffectManager.onEffectButtonClicked -= OnEffectButtonClicked;
        EffectManager.onEffectTextCommitted -= TextCommitted;

        Rocket.Core.Logging.Logger.Log("LTBenzinlik plugini pasif.");
        Rocket.Core.Logging.Logger.Log("Satın alım için teşekkürler.");
        isRunning = false;
    }

    private async void StartPositionCheckLoop()
    {
        while (isRunning)
        {
            foreach (var player in Provider.clients)
            {
                var unturnedPlayer = UnturnedPlayer.FromSteamPlayer(player);
                bool isInVehicle = unturnedPlayer.Player.movement.getVehicle() != null;

                foreach (var station in fuelStations)
                {
                    float distance = Vector3.Distance(unturnedPlayer.Position, station.Position);

                    if (distance < station.Radius)
                    {
                        if (isInVehicle)
                        {
                            // Oyuncu araçtaysa 17724 efekti ver
                            EffectManager.sendUIEffect(17724, 17724, unturnedPlayer.CSteamID, true);
                            EffectManager.sendUIEffectText(17724, unturnedPlayer.CSteamID, true, "BenzinlikAdi2", station.Name);
                            EffectManager.sendUIEffectText(17724, unturnedPlayer.CSteamID, true, "LitresiKacPara", $"{station.LitreFiyati}$");

                            if (Input.GetKeyDown(KeyCode.H)) // Korna kontrolü
                            {
                                EffectManager.askEffectClearByID(17724, unturnedPlayer.CSteamID);
                                EffectManager.sendUIEffect(17823, 17823, unturnedPlayer.CSteamID, true);

                                Task.Run(async () =>
                                {
                                    float fuelAdded = 0;
                                    while (unturnedPlayer.Experience >= station.LitreFiyati && unturnedPlayer.Player.movement.getVehicle() != null)
                                    {
                                        unturnedPlayer.Experience -= (uint)station.LitreFiyati;
                                        fuelAdded += 1.0f;

                                        EffectManager.sendUIEffectText(17823, unturnedPlayer.CSteamID, true, "AlinanBenzinFiyat", $"Fiyat: {station.LitreFiyati * fuelAdded}TL");
                                        EffectManager.sendUIEffectText(17823, unturnedPlayer.CSteamID, true, "AlinanBenzinLitre", $"Litre: {fuelAdded}L");

                                        if (unturnedPlayer.Experience < station.LitreFiyati)
                                        {
                                            UnturnedChat.Say(unturnedPlayer, "Yeterli paranız yok, işlem iptal edildi.", Color.red);
                                            EffectManager.askEffectClearByID(17823, unturnedPlayer.CSteamID);
                                            break;
                                        }
                                        await Task.Delay(1000);
                                    }
                                });
                            }
                            continue;
                        }

                        if (exitCooldowns.ContainsKey(unturnedPlayer.CSteamID) && (DateTime.Now - exitCooldowns[unturnedPlayer.CSteamID]).TotalSeconds < 5)
                        {
                            continue;
                        }

                        if (!effectCooldowns.ContainsKey(unturnedPlayer.CSteamID) || (DateTime.Now - effectCooldowns[unturnedPlayer.CSteamID]).TotalSeconds >= 3)
                        {
                            SendFuelStationUI(unturnedPlayer, station);
                            effectCooldowns[unturnedPlayer.CSteamID] = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (!exitCooldowns.ContainsKey(unturnedPlayer.CSteamID))
                        {
                            exitCooldowns[unturnedPlayer.CSteamID] = DateTime.Now;
                        }
                        else
                        {
                            EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
                        }
                    }
                }
            }
            await Task.Delay(1000);
        }
    }

    private void SendFuelStationUI(UnturnedPlayer player, FuelStation station)
    {
        EffectManager.sendUIEffect(23299, 23299, player.CSteamID, true);

        string currentDate = DateTime.Now.ToString("dd.MM.yyyy");
        string closestLocation = GetClosestLocationName(player);

        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "Tarih", currentDate);
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "BenzinlikFiyat", $"{station.Price}$");
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "BolgeYakinlik", $"{closestLocation}");
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "BenzinlikAdi", station.Name);
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "LitresiKacPara", $"{station.LitreFiyati}$");
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "BenzinlikAdi2", station.Name);

        EffectManager.sendUIEffectVisibility(23299, player.CSteamID, true, "BenzinlikSatinAlma", player.CharacterName != station.Owner);
        EffectManager.sendUIEffectVisibility(23299, player.CSteamID, true, "BenzinlikKontrol", player.CharacterName == station.Owner);
        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "OyuncuAdi", player.CharacterName);

        player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
        player.Player.setPluginWidgetFlag(EPluginWidgetFlags.ForceBlur, true);

        EffectManager.sendUIEffectText(23299, player.CSteamID, true, "KazanilanPara", $"{station.KazanilanPara}$");
    }

    private void TextCommitted(Player caller, string buttonName, string text)
    {
        var localPlayer = UnturnedPlayer.FromPlayer(caller);

        if (buttonName == "TransferKisi")
        {
            if (string.IsNullOrEmpty(text))
            {
                UnturnedChat.Say(localPlayer, "Bir Oyuncu Ismi Girmelisin", Color.red);
                return;
            }
            else
            {
                searchedUnturnedPlayer = text;
                UnturnedChat.Say(localPlayer, $"Transfer işlemleri sadece yetkililer tarafından yapılabilir");
            }
        }
    }

    private void OnEffectButtonClicked(Player player, string buttonName)
    {
        var unturnedPlayer = UnturnedPlayer.FromPlayer(player);
        var closestStation = GetClosestFuelStation(unturnedPlayer);

        if (buttonName == "SatinAlButon")
        {
            if (closestStation != null)
            {
                if (closestStation.Owner != null)
                {
                    EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
                    UnturnedChat.Say(unturnedPlayer, "Bu benzinlik zaten birine ait.", Color.red);
                    return;
                }

                if (unturnedPlayer.Experience < closestStation.Price)
                {
                    EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
                    UnturnedChat.Say(unturnedPlayer, "Yeterli deneyim puanınız yok.", Color.red);
                    return;
                }

                BuyFuelStation(unturnedPlayer, closestStation);
                UnturnedChat.Say(unturnedPlayer, $"{closestStation.Name} benzinliğini satın aldınız!");

                EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
                unturnedPlayer.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
                unturnedPlayer.Player.setPluginWidgetFlag(EPluginWidgetFlags.ForceBlur, false);
            }
            else
            {
                UnturnedChat.Say(unturnedPlayer, "Yakınlarda bir benzinlik bulunamadı.");
            }
        }
        else if (buttonName == "Cikis" || buttonName == "Cikis1")
        {
            if (closestStation != null)
            {
                exitCooldowns.Add(unturnedPlayer.CSteamID, DateTime.Now);

                EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
                unturnedPlayer.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
                unturnedPlayer.Player.setPluginWidgetFlag(EPluginWidgetFlags.ForceBlur, false);
            }
            else
            {
                EffectManager.askEffectClearByID(23299, unturnedPlayer.CSteamID);
            }
        }
    }

    private FuelStation GetClosestFuelStation(UnturnedPlayer player)
    {
        FuelStation closestStation = null;
        float closestDistance = float.MaxValue;

        foreach (var station in fuelStations)
        {
            float distance = Vector3.Distance(player.Position, station.Position);
            if (distance < station.Radius && distance < closestDistance)
            {
                closestStation = station;
                closestDistance = distance;
            }
        }

        return closestStation;
    }

    [RocketCommand("benzinlikekle", "", "/benzinlikekle <benzinlikadi> <fiyat> <radius> <litrefiyati>", AllowedCaller.Player)]
    public void AddFuelStation(IRocketPlayer caller, string[] command)
    {
        if (command.Length != 4)
        {
            UnturnedChat.Say(caller, "Kullanım: /benzinlikekle <benzinlikadi> <fiyat> <radius> <litrefiyati>");
            return;
        }

        var player = (UnturnedPlayer)caller;
        string name = command[0];
        float price;
        float radius;
        float litreFiyati;

        if (!float.TryParse(command[1], out price) || !float.TryParse(command[2], out radius) || !float.TryParse(command[3], out litreFiyati))
        {
            UnturnedChat.Say(caller, "Fiyat, radius ve litre fiyatı sayısal olmalı.");
            return;
        }

        FuelStation fuelStation = new FuelStation
        {
            Name = name,
            Price = price,
            Position = player.Position,
            Radius = radius,
            LitreFiyati = litreFiyati,
            Owner = null,
            CreationDate = DateTime.Now.ToString("dd.MM.yyyy")
        };

        fuelStations.Add(fuelStation);
        SaveConfig();
        UnturnedChat.Say(caller, $"{name} adlı benzinlik eklendi!");

        EffectManager.sendUIEffect(23299, 23299, player.CSteamID, true);
    }

    [RocketCommand("benzinliksil", "", "/benzinliksil <benzinlikadi>", AllowedCaller.Player)]
    public void RemoveFuelStation(IRocketPlayer caller, string[] command)
    {
        if (command.Length != 1)
        {
            UnturnedChat.Say(caller, "Kullanım: /benzinliksil <benzinlikadi>");
            return;
        }

        string name = command[0];
        var fuelStation = fuelStations.Find(fs => fs.Name == name);

        if (fuelStation == null)
        {
            UnturnedChat.Say(caller, $"{name} adlı benzinlik bulunamadı.");
            return;
        }

        fuelStations.Remove(fuelStation);
        SaveConfig();
        UnturnedChat.Say(caller, $"{name} adlı benzinlik silindi!");
    }

    public void BuyFuelStation(UnturnedPlayer player, FuelStation station)
    {
        player.Experience -= (uint)station.Price;
        station.Owner = player.CharacterName;
        SaveConfig();

        SendFuelStationUI(player, station);

        UnturnedChat.Say(player, $"{station.Name} benzinliğini satın aldınız!");
    }

    private async void StartParaBiriktirmeLoop()
    {
        while (isRunning)
        {
            foreach (var player in Provider.clients)
            {
                var unturnedPlayer = UnturnedPlayer.FromSteamPlayer(player);

                foreach (var station in fuelStations)
                {
                    if (station.Owner == unturnedPlayer.CharacterName)
                    {
                        if ((DateTime.Now - station.LastPaymentTime).TotalSeconds >= Configuration.Instance.ParaBiriktirmeSuresi)
                        {
                            station.KazanilanPara += Configuration.Instance.ParaMiktari;
                            station.LastPaymentTime = DateTime.Now;

                            EffectManager.sendUIEffectText(23299, unturnedPlayer.CSteamID, true, "KazanilanPara", $"{station.KazanilanPara}$");
                        }
                    }
                }
            }
            await Task.Delay(1000);
        }
    }

    private void SaveConfig()
    {
        Configuration.Instance.FuelStations = fuelStations;
        Configuration.Save();
    }

    private string GetClosestLocationName(UnturnedPlayer player)
    {
        LocationNode locationNode = LevelNodes.nodes.OfType<LocationNode>()
            .OrderBy(k => Vector3.Distance(k.point, player.Position))
            .FirstOrDefault();

        if (locationNode == null)
            return "Bilinmeyen lokasyon";

        return locationNode.name;
    }
}

public class FuelStation
{
    public string Name { get; set; }
    public float Price { get; set; }
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
    public float LitreFiyati { get; set; } // Litre fiyatını ekledik
    public string Owner { get; set; }
    public string CreationDate { get; set; }
    public float KazanilanPara { get; set; } = 0;
    public DateTime LastPaymentTime { get; set; }
}
