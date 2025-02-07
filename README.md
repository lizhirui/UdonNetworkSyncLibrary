# UdonNetworkSyncLibrary

[日本語バージョン](README_ja.md)

## 简介

本仓库实现了一个用于VRChat UdonSharp的网络同步库，该同步库相比于UdonSharp本身的`SendCustomNetworkEvent`而言，具有如下优势：

1. 对于任何RPC请求，可以传递任何复杂复合类型的参数（借助`UdonObjectSerializerLibrary`实现）。
2. 可以指定接收目标玩家，或直接广播发送给所有玩家。
3. 数据接收方可以得知发送方。
4. 任何人都具有发送权限，不需要关心所有权问题。
5. 支持Ping协议，可以测量玩家间的往返时延（RTT，Round-Trip Time），即消息从发送方到接收方并返回发送方所消耗的总时间。

相比于`UdonSynced`，则拥有如下优势：

1. 支持复杂复合类型（借助`UdonObjectSerializerLibrary`实现）。
2. 支持动态实例化对象（即由`Instantiate`方法直接克隆创建的对象）的变量网络同步。

## 组成结构

代码实现位于Assets/NetworkSync/UdonScript下，由三个.cs文件组成：NetworkSyncProcessor.cs、NetworkSyncPlayer.cs、NetworkSyncObject.cs，分别对应如下三个类：

* `NetworkSyncProcessor`：该类是整个网络同步库的核心，用于实现核心的数据传输同步和管理功能。
* `NetworkSyncPlayer`：该类负责给每个玩家提供数据发送接口，与每一个非`NetworkSyncProcessor`的所有者的成员相关联，仅被`NetworkSyncProcessor`对象调用，不直接被用户使用。
* `NetworkSyncObject`：提供对象变量网络接口和数据收发接口。

## 使用方法

### 导入

只需要将Assets目录中的NetworkSync目录放置到Unity工程的Assets目录中即可，要注意，该库同时依赖于`UdonObjectSerializerLibrary`，因此需要同时导入`UdonObjectSerializerLibrary`。

### NetworkSyncProcessor实例化

需要在Unity工程中适合的地方建立一个GameObject，并将`NetworkSyncProcessor`脚本绑定到上述GameObject上。

### NetworkSyncPlayer实例化

1. 在Unity工程中适合的地方建立一个GameObject，作为放置`NetworkSyncPlayer`对象集的根对象。
2. 在上述根对象下建立若干个GameObject，每个GameObject均需要绑定`NetworkSyncPlayer`对象，GameObject的数量为**房间最大人数-1**。
3. 将`NetworkSyncPlayer`对象集的根对象（即步骤一建立的GameObject）关联到`NetworkSyncProcessor`实例化步骤中建立的GameObject中的`NetworkSyncProcessor`脚本参数的`SyncPlayerObject`参数之上。

### NetworkSyncObject实例化

`NetworkSyncObject`支持以脚本绑定或继承方式使用，对于脚本绑定方式，所有的事件都需要转发到配置的`EventObject`处理，对于继承方式，则直接由继承事件处理方法来处理。

实例化步骤如下：

1. 在Unity工程中适合的地方建立一个GameObject或绑定到某个已有的GameObject上，对于数据发送场景，推荐绑定到用于数据发送的GameObject上，对于变量同步场景，推荐绑定到用于数据同步的GameObject上或让包含同步变量的类直接从`NetworkSyncObject`继承。
2. 将`SyncProcessor`参数与实际的`NetworkSyncPlayer`对象关联。
3. 如果是变量同步场景，则需要启用`GlobalMode`选项以启用变量同步功能支持（对于绑定在同一个`NetworkSyncPlayer`对象的若干`NetworkSyncObject`对象而言，非所有者的对象的`GlobalMode`的值会与`NetworkSyncObject`对象所有者同步）。
4. 如果需要其它对象接收网络事件（即非继承方式使用`NetworkSyncObject`），则将`EventObject`绑定到对应的`UdonSharpBehaviour`对象上，并正确配置`OnRPCReceivedName`、`OnPongReceivedName`、`OnGetValueEventName`、`OnSetValueEventName`、`OnGotOwnerEventName`这几个参数，以正确调用对应事件处理对象的方法（由`NetworkSyncObject`通过`EventObject`的`SendCustomEvent`触发）。

### 具体使用

用户所有功能均通过`NetworkSyncObject`对象暴露的方法及事件处理方法来实现，该对象遵循以下步骤使用：

#### 对象初始化

1. 如果`SyncProcessor`参数是在运行时动态配置（此时必须保证Editor中`SyncProcessor`参数为空）的，则需要在配置`SyncProcessor`参数后调用`NetworkSync_Init`进行对象初始化（只能执行一次，执行之后不能再修改`SyncProcessor`参数）。
2. 在执行一些关键操作时，需要分别调用`NetworkSync_IsReady`和`NetworkSync_IsNetworkRegistered`方法，检查当前对象的状态：
    * `NetworkSync_IsReady`仅用于检测`NetworkSyncObject`是否和`NetworkSyncProcessor`完成绑定，若未完成绑定，则不可发送数据，也无法同步变量，此时对任何发送/同步方法的调用都不会生效。
    * `NetworkSync_IsNetworkRegistered`用于检测玩家本人是否已经完成网络注册（即VRChat完成网络初始化，并且该玩家被`NetworkSyncProcessor`所有者观测到并纳入管理）。

        如果该玩家尚未完成网络注册，则不保证同步对象的数据完整性，同时，也无法发送数据和主动同步变量，因为系统还未为此玩家分配专属的`NetworkSyncPlayer`对象。注意，所有者本身不需要获得自己的`NetworkSyncPlayer`对象，但需要负责在玩家加入或离开时进行`NetworkSyncPlayer`对象的分配和回收。

    在玩家尚未完成网络注册时就对与网络同步强关联的本地数据结构进行修改（例如在监听玩家加入/离开事件时立即修改数据），可能会导致数据不一致，甚至造成错误或崩溃。为避免此类问题，请务必确认`NetworkSync_IsNetworkRegistered`返回`true`后，再进行此类与网络同步密切相关的操作。

#### RPC相关操作

- **RPC请求发送：** 发送RPC请求时，只需要调用`NetworkSync_SendRPC`方法，该方法包含三个参数：
    1. dstPlayer：接收RPC请求的玩家，若为null，则表示广播发送，即所有玩家均能接收到该RPC请求。
    2. cmd：RPC请求命令名称，该名称不得包含以下字符：“,”、“:”、“;”，否则会造成网络同步库工作异常。
    3. arg：RPC请求参数，若指定为null，则表示不需要附带参数，否则可以指定为所有`UdonObjectSerializerLibrary`支持的类型（具体参见`UdonObjectSerializerLibrary`仓库的说明）。
- **RPC请求接收：** 接收RPC请求时，需要借助`OnRPCReceived`事件完成，若以脚本绑定方式实现，则需要在对应的事件处理对象中的`OnRPCReceived`事件处理方法中实现，若以继承方式实现，则只需要继承`NetworkSync_OnRPCReceived`方法即可（后续不再赘述，只会直接指出事件名称），该事件具有三个参数：
    1. `srcPlayer`（脚本绑定方式通过`NetworkSync_Arg_GetSrcPlayer`方法获得）：RPC请求来源玩家，保证不为null。
    2. `cmd`（脚本绑定方式通过`NetworkSync_Arg_GetCmd`方法获得）：RPC请求命令名称。
    3. `arg`（脚本绑定方式通过`NetworkSync_Arg_GetArg`方法获得）：RPC请求参数，可能为null。

#### Ping协议

- **Ping请求发送：** 发送Ping请求时，只需要调用`NetworkSync_SendPing`方法，该方法仅包含一个参数，即`dstPlayer`，表示发送的目标玩家，该参数可以为null，这样可以实现广播发送Ping请求。
- **Pong请求接收：** 在发送Ping请求后，收到Ping请求的玩家会发送Pong响应，这会触发`OnPongReceived`事件处理方法，该事件具有三个参数：
    1. `srcPlayer`（脚本绑定方式通过`NetworkSync_Arg_GetSrcPlayer`方法获得）：RPC请求来源玩家，保证不为null。
    2. `startTime`（脚本绑定方式通过`NetworkSync_Arg_GetArg`方法获得object[]，并将0号成员转换成double类型）：Ping请求发出时的发送者端时间，单位为秒。
    3. `rtt`（脚本绑定方式通过`NetworkSync_Arg_GetArg`方法获得object[]，并将1号成员转换成int类型）：测量到的RTT值，单位为毫秒。

#### 网络同步变量

1. 首先通过`NetworkSync_RegisterProperty`方法注册一个变量（属性）名，该名称不得包含以下字符：“,”、“:”、“;”，否则会造成网络同步库工作异常。
2. 配置`OnGetValue`事件，该事件拥有一个参数`cmd`（脚本绑定方式通过`NetworkSync_Arg_GetCmd`方法获得），用于将待同步变量传到网络同步库中，用户根据`cmd`挑选出待同步变量，并直接返回（脚本绑定方式通过`NetworkSync_SetReturnValue`方法返回）变量（必须是`UdonObjectSerializerLibrary`支持的类型）。
3. 配置`OnSetValue`事件，该事件拥有两个参数，分别是`cmd`（脚本绑定方式通过`NetworkSync_Arg_GetCmd`方法获得）和`value`（脚本绑定方式通过`NetworkSync_Arg_GetArg`方法获得），用于将网络传来的数据同步到本地变量中。
4. 调用`NetworkSync_SyncValue`方法发起变量同步，被同步的变量必须已被注册且当前玩家是该`NetworkSyncObject`所属GameObject的所有者（这可以通过`NetworkSync_IsOwner`方法来检查，并可以通过`NetworkSync_GetOwner`获取当前所有者）。
5. `NetworkSyncObject`所有者可以通过修改`GlobalMode`属性实现变量同步的启用与禁用（非所有者不可在运行时修改该属性，否则会造成网络同步状态的不一致）。

#### NetworkSyncProcessor所有权获得检查

通过`OnGotOwner`事件可以检测到所属`NetworkSyncProcessor`对应GameObject对象的所有权转移给了自己，该事件没有参数。

## FAQ

### 为什么要引入`NetworkSyncPlayer`类？

这是因为，该库的实现是基于`[UdonSynced]`标记实现的，而该标记对应的同步变量只有所属GameObject的所有者才能同步，因此，正常情况下，如果要获得发送权限，则必须获得所有者权限才行。然而，这存在三个问题：

1. 频繁抢占所有者权限造成发送效率较低。
2. 经实测，若多方频繁抢占所有者，可能会造成死锁（目前发现的VRChat的Bug之一）：出现至少两个玩家同时获得所有者权限，从而引起网络通信彻底中断。
3. 若实现自定义的仲裁器，由当前所有者主动指派其它所有者。这需要较为复杂的网络通信和事件处理过程，经实测，这种方案的网络通信效率过低，故这种方案被放弃。

综上所述，引入了`NetworkSyncPlayer`类，为每个玩家独立预留一个GameObject作为所有者，并配合同一个`NetworkSyncProcessor`对象，使得所有玩家在同一时刻同时具有发送权限，这大大提升了网络通信效率。