using BlockTypes;
using ModLoaderInterfaces;
using UnityEngine;

namespace MoreBanners
{
    public class MoreBanners : IOnTryChangeBlock
    {
        public readonly string NewBannerName = "Khanx.NewBanner";

        public void OnTryChangeBlock(ModLoader.OnTryChangeBlockData data)
        {
            if (data.RequestOrigin.Type != BlockChangeRequestOrigin.EType.Player)
                return;

            Players.Player player = data.RequestOrigin.AsPlayer;

            //Placing a NEW banner
            if(data.TypeNew.Name.Equals(NewBannerName))
            {
                //Constrain 1: Must be inside of a colony
                if(player.ActiveColony == null)
                {
                    Chatting.Chat.Send(player, "<color=red>You must place the new banner inside of your colony</color>");
                    BlockCallback(data);

                    return;
                }

                Colony colony = player.ActiveColony;

                //Constrain 2: Distance to closer banner must be small than safe radius * 2
                bool minDistance = false;
                foreach (var banner in colony.Banners)
                {
                    if (ConnectedSafeArea(banner.Position, data.Position))
                        minDistance = true;
                }

                if (!minDistance)
                {
                    Chatting.Chat.Send(player, "<color=red>The safe area of the new banner must be connected with your colony.</color>");
                    
                    BlockCallback(data);
                    return;
                }

                //Constrain 2: 1.000.000 Colony Points
                if (!colony.TryTakePoints(10)) //11000000
                {
                    Chatting.Chat.Send(player, "<color=red>You need 1.000.000 Colony Points to place a new banner.</color>");

                    BlockCallback(data);
                    return;
                }

                data.TypeNew = BuiltinBlocks.Types.banner;
                data.RequestOrigin = new BlockChangeRequestOrigin(player, colony.ColonyID);
            }

            //Moving banner
            if (data.TypeNew.ItemIndex == BuiltinBlocks.Indices.banner)
            {
                Chatting.Chat.SendToConnected(data.CallbackOrigin.ToString());
                //Player is placing a NEW colony not a NEW banner to a existing colony
                if (player.ActiveColony == null || player.ActiveColony.Banners.Length == 1)
                {
                    return;
                }

                Colony colony = player.ActiveColony;

                //Constrain: Distance to closer banner must be < X
                bool minDistance = false;
                var closestBanner = colony.GetClosestBanner(data.Position);

                foreach (var banner in colony.Banners)
                {
                    if (closestBanner != banner &&  ConnectedSafeArea(banner.Position, data.Position))
                        minDistance = true;
                }

                if (!minDistance)
                {
                    Chatting.Chat.Send(player, "<color=red>The safe area of the new banner must be connected with your colony.</color>");

                    BlockCallback(data);
                    return;
                }
            }

            //Removing banner
            if (data.TypeOld.ItemIndex == BuiltinBlocks.Indices.banner && data.TypeNew.ItemIndex == BuiltinBlocks.Indices.air)
            {
                BlockEntities.Implementations.BannerTracker.Banner removedBanner = null;
                //If It is not possible to identify the banner then ignore
                if (!ServerManager.BlockEntityTracker.BannerTracker.TryGetClosest(data.Position, out removedBanner) || removedBanner == null)
                    return;

                //If the colony only has one or two banners then ignore
                if (removedBanner.Colony.Banners.Length < 3)
                    return;

                //Check if it is possible to arrive from Source to Destination without the removed banner
                int[] bannerGroups = new int[removedBanner.Colony.Banners.Length];
                int groups = 0;

                for(int i=0;i < removedBanner.Colony.Banners.Length;i++)
                {
                    for(int j = 0; j < removedBanner.Colony.Banners.Length;j++)
                    {
                        var source = removedBanner.Colony.Banners[i];
                        var destination = removedBanner.Colony.Banners[j];

                        if (source == destination || source == removedBanner || destination == removedBanner)
                            continue;

                        if(ConnectedSafeArea(source.Position,destination.Position))
                        {
                            if(bannerGroups[i] == 0 && bannerGroups[j] == 0)
                            {
                                groups++;
                                bannerGroups[i] = groups;
                                bannerGroups[j] = groups;
                            }
                            else if(bannerGroups[i] != 0 && bannerGroups[j] == 0)
                            {
                                bannerGroups[j] = bannerGroups[i];
                            }
                            else if (bannerGroups[j] != 0 && bannerGroups[i] == 0)
                            {
                                bannerGroups[i] = bannerGroups[j];
                            }
                            else
                            {
                                for (int k = 0; k < removedBanner.Colony.Banners.Length; k++)
                                {
                                    if (bannerGroups[k] == bannerGroups[i])
                                        bannerGroups[k] = bannerGroups[j];
                                }
                            }
                        }
                    }
                }

                int group = 0;
                for (int i = 0; i < removedBanner.Colony.Banners.Length; i++)
                {
                    if (bannerGroups[i] == 0)
                    {
                        if (removedBanner.Colony.Banners[i] != removedBanner)
                        {
                            Chatting.Chat.Send(player, "<color=red>The safe area of the new banner must be connected with your colony.</color>");

                            BlockCallback(data);
                            return;
                        }
                    }
                    else if (group == 0)
                    {
                        group = bannerGroups[i];
                    }
                    else if(group != bannerGroups[i])
                    {
                        Chatting.Chat.Send(player, "<color=red>The safe area of the new banner must be connected with your colony.</color>");

                        BlockCallback(data);
                        return;
                    }
                }
            }
            
        }

        public static bool ConnectedSafeArea(Pipliz.Vector3Int position, Pipliz.Vector3Int source)
        {
            int maxPartAbs = (position - source).MaxPartAbs;

            return (maxPartAbs <= ServerManager.ServerSettings.Banner.SafeRadiusMaximum * 2 + 1);
        }

        public static void BlockCallback(ModLoader.OnTryChangeBlockData data)
        {
            data.CallbackState = ModLoader.OnTryChangeBlockData.ECallbackState.Cancelled;
            data.InventoryItemResults.Clear();
        }
    }
}
