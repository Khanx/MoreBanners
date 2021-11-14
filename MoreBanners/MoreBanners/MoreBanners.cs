using BlockTypes;
using ModLoaderInterfaces;
using System;
using UnityEngine;
using static BlockEntities.Implementations.BannerTracker;

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
            if (data.TypeNew.Name.Equals(NewBannerName))
            {
                //Constrain 1: Must be inside of a colony
                if (player.ActiveColony == null)
                {
                    Chatting.Chat.Send(player, "<color=red>You must place the new banner inside of your colony</color>");
                    BlockCallback(data);

                    return;
                }

                Colony colony = player.ActiveColony;

                //Constrain 2: Distance to closer banner must be small than safe radius * 2
                if (!PermissionsManager.HasPermission(player, "khanx.placebanner")) //Permission to ignore the distance
                {
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
                }

                //Constrain 2: No place a banner close to another colony
                if (CollisionWithAnotherColony(data.Position, colony.ColonyID))
                {
                    Chatting.Chat.Send(player, "<color=red>Too close to another colony!</color>");

                    BlockCallback(data);
                    return;
                }

                //Constrain 3: 1.000.000 Colony Points
                if (!colony.TryTakePoints(1000000)) //1.000.000
                {
                    Chatting.Chat.Send(player, "<color=red>You need 1.000.000 Colony Points to place a new banner.</color>");

                    BlockCallback(data);
                    return;
                }


                data.TypeNew = BuiltinBlocks.Types.banner;
                data.RequestOrigin = new BlockChangeRequestOrigin(player, colony.ColonyID);
                data.InventoryItemResults.Add(new InventoryItem());
            }

            //Moving banner
            if (data.TypeNew.ItemIndex == BuiltinBlocks.Indices.banner)
            {
                //Player is placing a NEW colony or the colony only has one banner
                if (player.ActiveColony == null || player.ActiveColony.Banners.Length == 1 || data.InventoryItemResults.Count == 1)
                {
                    return;
                }

                Colony colony = player.ActiveColony;

                var moveBanner = colony.GetClosestBanner(data.Position);

                //Moving the banner will result in a isolatedBanner
                if (!PermissionsManager.HasPermission(player, "khanx.placebanner")) //Permission to ignore the distance
                {
                    if (!CanRemove(moveBanner, colony))
                    {
                        Chatting.Chat.Send(player, "<color=red>The banner cannot be moved to the new position because it would result in a discontinuous safe zone.</ color>");

                        BlockCallback(data);
                        return;
                    }

                    //The new position to place the banner must be connected with the safe area
                    bool minDistance = false;
                    foreach (var banner in colony.Banners)
                    {
                        if (banner != moveBanner)
                            if (ConnectedSafeArea(banner.Position, data.Position))
                                minDistance = true;
                    }

                    if (!minDistance)
                    {
                        Chatting.Chat.Send(player, "<color=red>The banner cannot be moved to the new position because it would result in another banner disconnected from the safe zone.</ color>");

                        BlockCallback(data);
                        return;
                    }
                }

                //New position collides with an existing colony
                if (CollisionWithAnotherColony(data.Position, colony.ColonyID))
                {
                    Chatting.Chat.Send(player, "<color=red>Too close to another colony!</color>");

                    BlockCallback(data);
                    return;
                }

                data.InventoryItemResults.Add(new InventoryItem());
            }

            //Removing banner
            if (data.TypeOld.ItemIndex == BuiltinBlocks.Indices.banner && data.TypeNew.ItemIndex == BuiltinBlocks.Indices.air)
            {
                //Moving the banner has already checked this conditions
                if (data.InventoryItemResults.Count == 1)
                {
                    return;
                }

                //If It is not possible to identify the banner then ignore
                if (!ServerManager.BlockEntityTracker.BannerTracker.TryGetClosest(data.Position, out Banner removedBanner) || removedBanner == null)
                    return;

                //If the colony only has less than 2 banners there is no problem
                if (removedBanner.Colony.Banners.Length < 3)
                    return;

                if (!PermissionsManager.HasPermission(player, "khanx.placebanner")) //Permission to ignore the distance
                {
                    if (!CanRemove(removedBanner, removedBanner.Colony))
                    {
                        Chatting.Chat.Send(player, "<color=red>The banner cannot be removed because it would result in another banner disconnected from the safe zone.</color>");

                        BlockCallback(data);
                        return;
                    }
                }
            }

        }

        public static bool CanRemove(Banner removeBanner, Colony colony)
        {
            if (colony.Banners.Length < 3)
                return true;

            int[] bannerGroups = new int[colony.Banners.Length];
            int groups = 0;

            for (int i = 0; i < colony.Banners.Length; i++)
            {
                for (int j = 0; j < colony.Banners.Length; j++)
                {
                    var source = colony.Banners[i];
                    var destination = colony.Banners[j];

                    if (source == destination || source == removeBanner || destination == removeBanner)
                        continue;

                    if (ConnectedSafeArea(source.Position, destination.Position))
                    {
                        if (bannerGroups[i] == 0 && bannerGroups[j] == 0)
                        {
                            groups++;
                            bannerGroups[i] = groups;
                            bannerGroups[j] = groups;
                        }
                        else if (bannerGroups[i] != 0 && bannerGroups[j] == 0)
                        {
                            bannerGroups[j] = bannerGroups[i];
                        }
                        else if (bannerGroups[j] != 0 && bannerGroups[i] == 0)
                        {
                            bannerGroups[i] = bannerGroups[j];
                        }
                        else
                        {
                            for (int k = 0; k < colony.Banners.Length; k++)
                            {
                                if (bannerGroups[k] == bannerGroups[i])
                                    bannerGroups[k] = bannerGroups[j];
                            }
                        }
                    }
                }
            }

            int group = 0;
            for (int i = 0; i < colony.Banners.Length; i++)
            {
                if (bannerGroups[i] == 0)
                {
                    if (colony.Banners[i] != removeBanner)
                    {
                        return false;
                    }
                }
                else if (group == 0)
                {
                    group = bannerGroups[i];
                }
                else if (group != bannerGroups[i])
                {
                    return false;
                }
            }

            return true;
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

        public static bool CollisionWithAnotherColony(Vector3Int position, int colonyID)
        {
            return ServerManager.BlockEntityTracker.BannerTracker.Positions.TryGetClosestWhere(position, BannerFromOtherColony, ref colonyID, out Pipliz.Vector3Int found, out Banner foundInstance, ServerManager.ServerSettings.Banner.SafeRadiusMaximum * 2 + 2);
        }

        private static bool BannerFromOtherColony(Pipliz.Vector3Int position, Banner banner, ref int colonyID)
        {
            if (banner == null || banner.Colony == null)
                return false;

            return banner.Colony.ColonyID != colonyID;
        }
    }
}
