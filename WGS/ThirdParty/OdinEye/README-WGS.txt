OdinEye files bundled with WGS (embedded into WindowsGameServer.dll, extracted to <Valheim server>\BepInEx\plugins\OdinEye
only when a server's "Player data via OdinEye" setting is on — see WGS/Games/ValheimOdinEye.cs).

OdinEye.dll, OdinEye.Models.dll
  Built by the WGS author from the upstream source, https://github.com/sparcopt/odin-eye (tag v1.0.0, MIT licence, see
  OdinEye-LICENSE.txt). Upstream publishes no binaries. Built unmodified in an SDK-style copy of the project
  (TargetFramework net481, ValheimGameLibs 0.217.38 from nuget.bepinex.dev for compile-time references only).
  Known issue with the current Valheim: the Chat.OnNewChatMessage Harmony patch fails to apply (changed signature);
  the /players endpoint WGS uses works.

Runtime dependencies copied from that build (none of the game/Unity stub assemblies are included):
  NGuid (MIT), Utf8Json (MIT), WebsocketSharp.Core (websocket-sharp.core, MIT), protobuf-net + protobuf-net.Core (Apache-2.0 / MIT),
  System.Buffers, System.Collections.Immutable, System.Memory, System.Numerics.Vectors,
  System.Runtime.CompilerServices.Unsafe, System.Threading.Tasks.Extensions, System.ValueTuple (Microsoft, MIT).

BepInEx is NOT bundled (LGPL-2.1): WGS downloads BepInExPack_Valheim 5.4.2333 from Thunderstore at install time.
