﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PassiveBOT.Commands
{
    public class InvitePasssiveBOT : ModuleBase
    {
        [Command("invite"), Summary("invite"), Remarks("Returns the OAuth2 Invite URL of the bot")]
        public async Task Invite()
        {
            var application = await Context.Client.GetApplicationInfoAsync();
            await ReplyAsync($"A user with `MANAGE_SERVER` can invite me to your server here: <https://discordapp.com/oauth2/authorize?client_id={application.Id}&scope=bot&permissions=2146958591>");

        }
    }

    [RequireUserPermission(GuildPermission.ManageRoles)]
    public class Admin : ModuleBase
    {
        [Command("qc"), Summary("qc '@role' '60'"), Remarks("quickcolour role")]
        public async Task Quickcolour([Optional] IRole role, [Optional] int timeout)
        {
            if (role == null)
            {
                await ReplyAsync("**ERROR: **Please Specify a role and an interval eg. `.qc @LSD 60`");
            }
            else if (timeout == 0)
            {
                await ReplyAsync("**ERROR: ** Please specify a role an interval eg. `.qc @LSD 60`");
            }
            else if (timeout >= 1 && timeout < 60)
            {
                await ReplyAsync("**ERROR: ** Minimum interval must be 60 eg. `.qc @LSD 60`");
            }
            else
            {
                var quick = await ReplyAsync(".colour " + timeout + " " + role + " 9400D3 4B0082 0000FF 00FF00 FFFF00 FF7F00 FF0000");
                await quick.DeleteAsync();
            }
        }
        [Command("qcoff"), Alias("qc off"), Summary("qcoff '@role'"), Remarks("turns quickcolour role off")]
        public async Task Qoff([Optional] IRole role)
        {
            if (role == null)
            {
                await ReplyAsync("**ERROR: **Please Specify a role and an interval eg. `.qc @LSD 60`");
            }
            else
            {
                var quick = await ReplyAsync(".colour 0 " + role);
                await quick.DeleteAsync();
            }
        }
        private static ConcurrentDictionary<ulong, Timer> _rotatingRoleColors = new ConcurrentDictionary<ulong, Timer>();
        [Command("Colour"), Summary("colour '60' '@role' 'FFFFFF FFFFF1'"), Remarks("Changes the Colour of a role")]
        public async Task Colour(int timeout, IRole role, params string[] hexes)
        {
            var channel = (ITextChannel)Context.Channel;

            if ((timeout < 60 && timeout != 0) || timeout > 3600)
                return;

            Timer t;
            if (timeout == 0 || hexes.Length == 0)
            {
                if (_rotatingRoleColors.TryRemove(role.Id, out t))
                {
                    t.Change(Timeout.Infinite, Timeout.Infinite);
                    await ReplyAsync("**Stopped Rotating Colours for**\n**Role:** " + role);
                }
                return;
            }
            var hexColors = hexes.Select(hex => {
                try
                {
                    return (ImageSharp.Color?)ImageSharp.Color.FromHex(hex.Replace("#", ""));
                }
                catch
                {
                    return null;
                }
            })
             .Where(c => c != null)
             .Select(c => c.Value)
             .ToArray();

            if (!hexColors.Any())
            {
                return;
            }

            var images = hexColors.Select(color => {
                var img = new ImageSharp.Image(50, 50);
                img.BackgroundColor(color);
                return img;
            });

            var i = 0;
            t = new Timer(async (_) => {
                try
                {
                    var color = hexColors[i];
                    await role.ModifyAsync(r => r.Color = new Discord.Color(color.R, color.G, color.B)).ConfigureAwait(false);
                    ++i;
                    if (i >= hexColors.Length)
                        i = 0;
                }
                catch { }
            }, null, 0, timeout * 1000);

            _rotatingRoleColors.AddOrUpdate(role.Id, t, (key, old) => {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await ReplyAsync("**Rotating Colours for**\n**Role:** " + role + "\n**Interval:** " + timeout + " second(s)");
        }

        [Command("prune"), Summary("prune"), Remarks("removes all the bots recent messages")]
        public async Task Prune()
        {
            var user = await Context.Guild.GetCurrentUserAsync().ConfigureAwait(false);

            var enumerable = (await Context.Channel.GetMessagesAsync().Flatten()).AsEnumerable();
            enumerable = enumerable.Where(x => x.Author.Id == user.Id);
            await Context.Channel.DeleteMessagesAsync(enumerable).ConfigureAwait(false);
        }
        [Command("clear"), Summary("clear 26"), Remarks("removes the specified amount of messages")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Clear([Optional] int count)
        {
            if (count < 1)
            {
                await ReplyAsync("**ERROR: **Please Specify the amount of messages you want to clear");
                return;
            }
            else if (count > 100)
            {
                await ReplyAsync("**Error: **I can only clear 100 Messages at a time!");
            }
            else
            {
                await Context.Message.DeleteAsync().ConfigureAwait(false);
                int limit = (count < 100) ? count : 100;
                var enumerable = (await Context.Channel.GetMessagesAsync(limit: limit).Flatten().ConfigureAwait(false));
                await Context.Channel.DeleteMessagesAsync(enumerable).ConfigureAwait(false);
                await ReplyAsync($"Cleared **{count}** Messages");
            }
        }

        [Command("nopre"), Summary("nopre"), Remarks("toggles prefixless commands in the current server")]
        public async Task Nopre()
        {
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "moderation/prefix/")))
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "moderation/prefix/"));


            var lines = File.ReadAllLines(AppContext.BaseDirectory + $"moderation/prefix/nopre.txt");
            List<string> result = lines.ToList();
            if (result.Contains(Context.Guild.Id.ToString()))
            {
                var oldLines = File.ReadAllLines($"{AppContext.BaseDirectory + $"moderation/prefix/nopre.txt"}");
                var newLines = oldLines.Where(line => !line.Contains(Context.Guild.Id.ToString()));
                File.WriteAllLines($"{AppContext.BaseDirectory + $"moderation/prefix/nopre.txt"}", newLines);
                await ReplyAsync($"{Context.Guild} has been removed from the noprefix list (secret commands and prefixless commands are now enabled)");

            }
            else
            {
                File.AppendAllText($"{AppContext.BaseDirectory + $"moderation/prefix/nopre.txt"}", $"{Context.Guild.Id}" + Environment.NewLine);
                await ReplyAsync($"{Context.Guild} has been added to the noprefix list (secret commands and prefixless commands are now disabled)");
            }
        }

        [Command("kick"), Summary("kick '@badperson' 'for not being cool'"), Remarks("Kicks the specified user (requires Kick Permissions)")]
        [RequireContext(ContextType.Guild), RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kickuser(SocketGuildUser user, [Remainder, Optional] string reason)
        {
            if (user.GuildPermissions.ManageRoles == true)
            {
                await ReplyAsync($"**ERROR: **you cannot kick a a user with manage roles permission");
                return;
            }
            else if (reason == null)
            {
                await ReplyAsync("**ERROR: **Please specify a reason for Kicking the user");
                return;
            }
            else
            {
                await ReplyAsync($"{user.Mention} you have been kicked for `{reason}`:bangbang: ");
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync($"{user.Mention} you have been kicked from {Context.Guild} for `{reason}`");
                await user.KickAsync();

                var warnpath = Path.Combine(AppContext.BaseDirectory, $"moderation/kick/{Context.Guild.Id}.txt");
                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "moderation/kick/")))
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "moderation/kick/"));

                File.AppendAllText(AppContext.BaseDirectory + $"moderation/kick/{Context.Guild.Id}.txt", $"User: {user} || Moderator: {Context.User} || Reason: {reason}" + Environment.NewLine);
            }
        }

        [Command("warn"), Summary("warn '@naughtykiddo' 'for being a noob'"), Remarks("warns the specified user")]
        [RequireContext(ContextType.Guild)]
        public async Task NewWarnuser(SocketGuildUser user, [Remainder, Optional] string reason)
        {
            if (user.GuildPermissions.ManageRoles == true)
            {
                await ReplyAsync($"**ERROR: **you cannot warn a a user with manage roles permission");
                return;
            }
            else if (reason == null)
            {
                await ReplyAsync("**ERROR: **Please Specify a reason for warning the user");
                return;
            }
            else
            {
                await ReplyAsync($"{user.Mention} you have been warned for `{reason}`");
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync($"{user.Mention} you have been warned for `{reason}` in {Context.Guild}");


                var warnpath = Path.Combine(AppContext.BaseDirectory, $"moderation/warn/{Context.Guild.Id}.txt");
                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "moderation/warn/")))
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "moderation/warn/"));

               File.AppendAllText(AppContext.BaseDirectory + $"moderation/warn/{Context.Guild.Id}.txt", $"User: {user} || Moderator: {Context.User} || Reason: {reason}" + Environment.NewLine);

            }
            
        }

        [Command("ban"), Summary("ban 'badfag' 'for sucking'"), Remarks("bans the specified user (requires Ban Permissions)")]
        [RequireContext(ContextType.Guild), RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Banuser(SocketGuildUser user, [Remainder, Optional] string reason)
        {

            if (user.GuildPermissions.ManageRoles == true)
            {
                await ReplyAsync($"**ERROR: **you cannot ban a user with manage roles permission");
                return;
            }
            else if (reason == null)
            {
                await ReplyAsync("**ERROR: ** Please specify a reason for banning the user!");
                return;
            }
            else
            {
                await ReplyAsync($"{user.Mention} you have been banned for `{reason}`:bangbang: ");
                var dm = await user.CreateDMChannelAsync();
                await dm.SendMessageAsync($"{user.Mention} you have been banned from {Context.Guild} for `{reason}`");
                await Context.Guild.AddBanAsync(user);

                var warnpath = Path.Combine(AppContext.BaseDirectory, $"moderation/ban/{Context.Guild.Id}.txt");
                if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "moderation/ban/")))
                    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "moderation/ban/"));


                File.AppendAllText(AppContext.BaseDirectory + $"moderation/ban/{Context.Guild.Id}.txt", $"User: {user} || Moderator: {Context.User} || Reason: {reason}" + Environment.NewLine);

            }
        }

        [Command("mute"), Summary("mute '@loudboy'"), Remarks("Mutes the specified player")]
        public async Task Mute(string user, [Remainder, Optional] string reason)
        {
            if (Utilities.ManageRole((SocketGuild)Context.Guild) == null)
            {
                await ReplyAsync("you must have the `Manage Roles` Permission to use this command");
            }
            else if (Utilities.GetMutedRole((SocketGuild)Context.Guild) == null)
            {
                await ReplyAsync("This server does not contain the role 'Muted' type `.mutehelp` for more info");
            }
            else if (reason == null)
            {
                await ReplyAsync("**ERROR: **Please specify a reason type `.mutehelp` for more info");
            }
            else if (Context.Message.MentionedUserIds.Count != 0 && Context.Guild.GetUserAsync(Context.Message.MentionedUserIds.FirstOrDefault()) != null)
            {
                IGuildUser target = await Context.Guild.GetUserAsync(Context.Message.MentionedUserIds.FirstOrDefault());
                IRole muted = Utilities.GetMutedRole((SocketGuild)Context.Guild);
                if (target.RoleIds.Contains(muted.Id))
                {
                    await ReplyAsync("**ERROR: ** The user is already muted");
                }
                else
                {
                    await target.AddRoleAsync(muted);
                    await ReplyAsync(target.Username + " is muted.");
                }
            }
            else if (await Utilities.GetUser(Context.Guild, user) != null)
            {
                IGuildUser target = await Utilities.GetUser(Context.Guild, user);
                IRole muted = Utilities.GetMutedRole((SocketGuild)Context.Guild);
                if (target.RoleIds.Contains(muted.Id))
                {
                    await ReplyAsync("**ERROR: ** The user is already muted");
                }
                else
                {
                    await target.AddRoleAsync(muted);
                    await ReplyAsync(target.Username + " is muted.");
                }
            }
            else
            {
                await ReplyAsync("Who the fuck is " + user + "?");
            }
        }

        [Command("unmute"), Summary("unmute 'quiteboy'"), Remarks("unmutes the specified user")]
        public async Task Unmute(string user)
        {
            if (Utilities.ManageRole((SocketGuild)Context.Guild) == null)
                await ReplyAsync("you must have the `Manage Roles` Permission to use this command");
            else if (Utilities.GetMutedRole((SocketGuild)Context.Guild) == null)
                await ReplyAsync("This server does not contain the role 'Muted' type `.mutehelp` for more info");
            else if (Context.Message.MentionedUserIds.Count != 0 && Context.Guild.GetUserAsync(Context.Message.MentionedUserIds.FirstOrDefault()) != null)
            {
                IGuildUser target = await Context.Guild.GetUserAsync(Context.Message.MentionedUserIds.FirstOrDefault());
                IRole muted = Utilities.GetMutedRole((SocketGuild)Context.Guild);
                if (target.RoleIds.Contains(muted.Id))
                {
                    await target.RemoveRoleAsync(muted);
                    await ReplyAsync(target.Username + " is unmuted.");
                }
                else
                {
                    await ReplyAsync("**ERROR: ** The user wasn't muted in the first place");
                }
            }
            else if (await Utilities.GetUser(Context.Guild, user) != null)
            {
                IGuildUser target = await Utilities.GetUser(Context.Guild, user);
                IRole muted = Utilities.GetMutedRole((SocketGuild)Context.Guild);
                if (target.RoleIds.Contains(muted.Id))
                {
                    await target.RemoveRoleAsync(muted);
                    await ReplyAsync(target.Username + " is unmuted");
                }
                else
                {
                    await ReplyAsync("**ERROR: ** The user wasn't muted in the first place");
                }
            }
            else
            {
                await ReplyAsync("Who the fuck is " + user + "?");
            }
        }

        [Command("kicks"), Summary("kicks"), Remarks("Users kicked by passivebot")]
        public async Task Kicks()
        {
            var kicks = File.ReadAllText(AppContext.BaseDirectory + $"moderation/kick/{Context.Guild.Id}.txt");
            await ReplyAsync("```\n" + kicks + "\n```");
        }

        [Command("warns"), Summary("warns"), Remarks("Users warned by passivebot")]
        public async Task Warns()
        {
            var warns = File.ReadAllText(AppContext.BaseDirectory + $"moderation/warn/{Context.Guild.Id}.txt");
            await ReplyAsync("```\n" + warns + "\n```");
        }

        [Command("bans"), Summary("bans"), Remarks("Users banned by passivebot")]
        public async Task Bans()
        {
            var bans = File.ReadAllText(AppContext.BaseDirectory + $"moderation/ban/{Context.Guild.Id}.txt");
            await ReplyAsync("```\n" + bans + "\n```");
        }
    }
}