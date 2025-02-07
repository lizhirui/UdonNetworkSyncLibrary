# UdonNetworkSyncLibrary（翻訳者：fog8360）

## 概要

このリポジトリは、VRChat UdonSharp向けのネットワーク同期ライブラリを実装したものです。この同期ライブラリは、UdonSharpの`SendCustomNetworkEvent`と比較して、以下の利点があります：

1. どんなRPCリクエストでも、複雑な複合型の引数を渡すことができる（`UdonObjectSerializerLibrary`を使用）。
2. 受信対象のプレイヤーを指定することができる、またはすべてのプレイヤーにブロードキャストして送信することができる。
3. データ受信者は送信者を知ることができる。
4. 誰でも送信権限を持ち、所有権の問題を気にする必要がない。
5. Pingプロトコルをサポートしており、プレイヤー間の往復遅延（RTT、Round-Trip Time）を測定できる。これはメッセージが送信者から受信者に送られ、再び送信者に戻るのにかかる総時間を意味します。

`UdonSynced`と比較して、以下の利点があります：

1. 複雑な複合型をサポート（`UdonObjectSerializerLibrary`を使用）。
2. 動的インスタンス化されたオブジェクト（`Instantiate`メソッドで直接複製されたオブジェクト）の変数ネットワーク同期をサポート。

## 構成

コードは`Assets/NetworkSync/UdonScript`内にあり、3つの.csファイルで構成されています：`NetworkSyncProcessor.cs`、`NetworkSyncPlayer.cs`、`NetworkSyncObject.cs`。これらはそれぞれ以下のクラスに対応します：

* `NetworkSyncProcessor`：このクラスはネットワーク同期ライブラリのコアで、データ送受信と同期管理機能を実装しています。
* `NetworkSyncPlayer`：このクラスは各プレイヤーにデータ送信インターフェースを提供し、`NetworkSyncProcessor`の所有者でないメンバーに関連付けられ、`NetworkSyncProcessor`オブジェクトからのみ呼び出されます（ユーザーによる直接使用はありません）。
* `NetworkSyncObject`：オブジェクトの変数ネットワークインターフェースとデータ送受信インターフェースを提供します。

## 使用方法

### インポート

`Assets`ディレクトリ内の`NetworkSync`フォルダをUnityプロジェクトの`Assets`ディレクトリに配置するだけでインポートできます。このライブラリは`UdonObjectSerializerLibrary`にも依存しているため、`UdonObjectSerializerLibrary`も同時にインポートする必要があります。

### NetworkSyncProcessorのインスタンス化

Unityプロジェクト内で適切な場所に`GameObject`を作成し、`NetworkSyncProcessor`スクリプトをその`GameObject`にアタッチします。

### NetworkSyncPlayerのインスタンス化

1. Unityプロジェクト内で適切な場所に`GameObject`を作成し、`NetworkSyncPlayer`オブジェクトの親オブジェクトを作成します。
2. 上記の親オブジェクトの下にいくつかの`GameObject`を作成し、それぞれに`NetworkSyncPlayer`オブジェクトをアタッチします。`GameObject`の数は**ルームの最大人数-1**である必要があります。
3. 上記で作成した`NetworkSyncPlayer`オブジェクトの親オブジェクト（ステップ1で作成した`GameObject`）を、`NetworkSyncProcessor`インスタンス化時に作成した`GameObject`の`NetworkSyncProcessor`スクリプトの`SyncPlayerObject`パラメーターに関連付けます。

### NetworkSyncObjectのインスタンス化

`NetworkSyncObject`はスクリプトのバインディングまたは継承方式で使用できます。スクリプトバインディング方式では、すべてのイベントを指定した`EventObject`に転送する必要があります。継承方式では、継承したイベント処理メソッドが直接処理します。

インスタンス化手順は以下の通りです：

1. Unityプロジェクト内で適切な場所に`GameObject`を作成するか、既存の`GameObject`にバインドします。データ送信シーンでは、データ送信用の`GameObject`にバインドするのが推奨されます。変数同期シーンでは、変数同期用の`GameObject`または同期変数を含むクラスが`NetworkSyncObject`を継承することが推奨されます。
2. `SyncProcessor`パラメータを実際の`NetworkSyncPlayer`オブジェクトに関連付けます。
3. 変数同期シーンの場合、`GlobalMode`オプションを有効にして、変数同期機能をサポートします（同じ`NetworkSyncPlayer`オブジェクトにバインドされている複数の`NetworkSyncObject`は、非所有者の`GlobalMode`の値を所有者と同期します）。
4. 他のオブジェクトがネットワークイベントを受信する必要がある場合（`NetworkSyncObject`を継承せずに使用する場合）、`EventObject`を対応する`UdonSharpBehaviour`オブジェクトにバインドし、`OnRPCReceivedName`、`OnPongReceivedName`、`OnGetValueEventName`、`OnSetValueEventName`、`OnGotOwnerEventName`のパラメータを正しく設定して、対応するイベント処理メソッドを呼び出します（`NetworkSyncObject`は`EventObject`を使って`SendCustomEvent`を呼び出します）。

### 具体的な使用方法

すべての機能は`NetworkSyncObject`オブジェクトのメソッドおよびイベント処理メソッドを通じて提供され、以下の手順で使用します：

#### オブジェクトの初期化

1. `SyncProcessor`パラメータがランタイム中に動的に設定されている場合（この場合、Editor内で`SyncProcessor`パラメータが空である必要があります）、`SyncProcessor`パラメータを設定した後、`NetworkSync_Init`を呼び出してオブジェクトを初期化します（これは一度だけ実行可能で、その後`SyncProcessor`パラメータを変更することはできません）。
2. いくつかの重要な操作を実行する前に、`NetworkSync_IsReady`および`NetworkSync_IsNetworkRegistered`メソッドを呼び出して、現在のオブジェクトの状態を確認します：
    * `NetworkSync_IsReady`は`NetworkSyncObject`が`NetworkSyncProcessor`と正常にバインドされているかどうかを確認するために使用されます。バインドされていない場合、データ送信や変数同期は行われません。その場合、送信/同期メソッドを呼び出しても効果はありません。
    * `NetworkSync_IsNetworkRegistered`はプレイヤーがネットワークに登録されているかどうかを確認するために使用されます（VRChatがネットワーク初期化を完了し、プレイヤーが`NetworkSyncProcessor`によって監視されている状態）。ネットワーク登録が完了していない場合、同期オブジェクトのデータの完全性が保証されません。また、データの送信や変数の同期もできません。

    プレイヤーがまだネットワーク登録を完了していない状態で、ネットワーク同期と密接に関連したローカルデータ構造を変更することは、データ不一致やエラー、クラッシュの原因になる可能性があります。このような問題を避けるため、`NetworkSync_IsNetworkRegistered`が`true`を返すことを確認した後に、ネットワーク同期に関連する操作を行ってください。

#### RPC関連操作

- **RPCリクエスト送信：** RPCリクエストを送信する際は、`NetworkSync_SendRPC`メソッドを呼び出すだけです。このメソッドは3つのパラメータを受け取ります：
    1. `dstPlayer`：RPCリクエストを受信するプレイヤー。`null`の場合、ブロードキャスト送信となり、すべてのプレイヤーがRPCリクエストを受け取ります。
    2. `cmd`：RPCリクエストのコマンド名。この名称には以下の文字を含めてはいけません： `","`、`":"`、`";"`、これらを含むとネットワーク同期ライブラリが正常に動作しません。
    3. `arg`：RPCリクエストの引数。`null`を指定すると引数なしとなります。それ以外の場合は、`UdonObjectSerializerLibrary`でサポートされている任意の型を指定できます。
- **RPCリクエストの受信：** RPCリクエストを受信するには、`OnRPCReceived`イベントを使用します。スクリプトバインディング方式の場合は、対応するイベント処理オブジェクトの`OnRPCReceived`イベント処理メソッドに実装を行います。継承方式の場合は、`NetworkSync_OnRPCReceived`メソッドを継承するだけです（後述ではイベント名のみ記載します）。このイベントは以下の3つのパラメータを持ちます：
    1. `srcPlayer`（スクリプトバインディング方式では`NetworkSync_Arg_GetSrcPlayer`メソッドを使用して取得）：RPCリクエストの送信元プレイヤー。`null`ではありません。
    2. `cmd`（スクリプトバインディング方式では`NetworkSync_Arg_GetCmd`メソッドを使用して取得）：RPCリクエストのコマンド名。
    3. `arg`（スクリプトバインディング方式では`NetworkSync_Arg_GetArg`メソッドを使用して取得）：RPCリクエストの引数。`null`の可能性もあります。

#### Pingプロトコル

- **Pingリクエスト送信：** Pingリクエストを送信するには、`NetworkSync_SendPing`メソッドを呼び出します。このメソッドは1つのパラメータ（送信先プレイヤー）を受け取ります。`null`にすると、Pingリクエストはブロードキャストされます。
- **Pongリクエストの受信：** Pingリクエストを送信した後、Pingリクエストを受けたプレイヤーがPongレスポンスを送信します。これが`OnPongReceived`イベントをトリガーし、このイベントには以下の3つのパラメータがあります：
    1. `srcPlayer`（スクリプトバインディング方式では`NetworkSync_Arg_GetSrcPlayer`メソッドを使用して取得）：Pingリクエストの送信元プレイヤー。`null`ではありません。
    2. `startTime`（スクリプトバインディング方式では`NetworkSync_Arg_GetArg`メソッドを使用して`object[]`として取得し、0番目の要素を`double`型に変換）：Pingリクエストが送信されたときの送信者側の時間（秒単位）。
    3. `rtt`（スクリプトバインディング方式では`NetworkSync_Arg_GetArg`メソッドを使用して`object[]`として取得し、1番目の要素を`int`型に変換）：測定されたRTT（往復遅延）値（ミリ秒単位）。

#### ネットワーク同期変数

1. `NetworkSync_RegisterProperty`メソッドを使用して変数（プロパティ）の名前を登録します。この名称には以下の文字を含めてはいけません： `","`、`":"`、`";"`、これらを含むとネットワーク同期ライブラリが正常に動作しません。
2. `OnGetValue`イベントを設定します。このイベントには1つのパラメータ`cmd`（スクリプトバインディング方式では`NetworkSync_Arg_GetCmd`メソッドを使用して取得）があり、ネットワーク同期ライブラリに同期する変数を渡します。ユーザーは`cmd`に基づいて同期すべき変数を選択し、`NetworkSync_SetReturnValue`メソッドを使って返します。
3. `OnSetValue`イベントを設定します。このイベントには2つのパラメータがあります：`cmd`（スクリプトバインディング方式では`NetworkSync_Arg_GetCmd`メソッドを使用）と`value`（スクリプトバインディング方式では`NetworkSync_Arg_GetArg`メソッドを使用）。
4. `NetworkSync_SyncValue`メソッドを呼び出して変数同期を開始します。同期される変数は登録されており、現在のプレイヤーがその`NetworkSyncObject`に関連付けられた`GameObject`の所有者である必要があります（これを`NetworkSync_IsOwner`メソッドで確認できます）。
5. `NetworkSyncObject`の所有者は`GlobalMode`属性を変更することで変数同期の有効化と無効化を管理できます（所有者でないプレイヤーはランタイム中にこの属性を変更できません）。

#### NetworkSyncProcessorの所有権確認

`OnGotOwner`イベントを使用して、`NetworkSyncProcessor`が関連付けられた`GameObject`の所有権が自分に移行したことを検出できます。このイベントにはパラメータはありません。