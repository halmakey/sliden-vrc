# Sliden

## 環境

* VRCSDK3
* VRChat Creator Companion
* UdonSharp

## 概要

VRChatのワールド上にプレゼンテーション用のスライドを表示するギミックです。
スライドのページがインスタンス内のすべてのユーザーに同期して表示されます。
Quest・PC両対応です。

## 機能

Slidenは次の機能を提供します。

* 動画を1秒1ページのスライドとして他ユーザーと同期して再生する
* スピーカータブレットでスライドを操作する
  - 現在のページを表示する
  - 初期スライドに戻る
  - 再生するスライドのURLを入力する
  - スライドを再読み込みする（ローカル）
  - ページをめくる
  - スクリーンの表示／非表示を切り替える
  - チャイムを鳴らす
  - タブレットの位置をロックする
* ローカルタブレットでスライドを閲覧する
  - 現在のページを表示する
  - スライドを再読み込みする
  - タブレットの位置をロックする

## 制限事項

SlidenはClientSimに対応していないため、Unity Editor上では動作しません。
動作確認の際はVRChat SDKのBuilderにあるBuild & Testボタンからご確認ください。

## スライドデータ

Slidenはスライドのデータとして、Web上に配置された動画ファイルを使用します。

https://sliden.app/ にてPDFや画像ファイルを簡単に変換して動画ファイルとしてWeb上に公開できますのでご利用ください。

自前のサーバに配置する場合は、解像度1280x720のmp4形式で、ページ内容が1秒毎に切り替わるスライドショーになっているものを使用してください。
Youtube等にアップロードすることもできますが、Youtubeにアップロードされたスライド動画はQuestでは再生できないためご注意ください。

mp4ファイルのURLを直接タブレットに入力すると再生されます。
スライドの再生中はページが自動的に移動することはありません。
タブレットの左右ボタンで発表者がページのナビゲーションを行ってください。

# 初期表示スライド

「Sliden」オブジェクトの「Initial Url」で指定されたURLを初期表示スライドとして再生します。

他のビデオプレイヤーの動画再生とタイミングが被るとスライド表示や動画再生に失敗します。
これを回避するには「Wait For First Load」に初期ロードの待機秒数を入力してください。

## 導入方法

1. UnityPackageから内容物をすべてインポートしてください
2. 使いたいシーンにSliden.prefabを追加してください。
3. Slidenオブジェクト内の「MainPanel」と「SpeakerDock」、「LocalDock」をそれぞれ好きな位置に移動してください

SlidenSampleScnene.unityにて実際にSlidenを配置した例があります。

## スクリーン（MainScreen）を追加する

スクリーンには現在再生中のスライドが表示されます。
スクリーンが追加で必要な場合は次の手順で追加してください。

1. シーンにアスペクト比 16:9のサイズでQuadを配置する
2. Quadのマテリアルに「SlidenScreenMaterial.mat」を設定する
3. Quadにコンポーネント「VRC AVPro Video Screen」を追加する
4. Quadに「VRC AVPro Video Screen」コンポーネントを追加し、「Video Player」に「Sliden」オブジェクトを指定する
5. Quadに「Screen (U# Script)」コンポーネントを追加し、「Sliden」に「Sliden」オブジェクトを指定する
6. Quadに「Hidable (U# Script)」コンポーネントを追加し、「Sliden」に「Sliden」オブジェクトを指定する

## ローカルタブレット（LocalDock）を追加する

ローカルタブレットはスライド発表者以外のユーザーが使用するためのタブレットです。
タブレットは掴んで自由な位置に移動する事ができますが、位置や状態はそれぞれのユーザーで同期されません。（ローカル）

ローカルタブレットを使ってできること:
- 現在のページを表示する
- スライドを再読み込みする
- タブレットの位置をロックする

また、ローカルタブレットは次の手順で複製して複数設置できます。

1. ワールド上に設置したSlidenプレハブの中で「LocalDock」オブジェクトを選択し、右クリックメニューから「Duplicate」でLocalDockを複製する
2. 複製された「LocalDock (1)」を好きな位置に移動する

## プレースホルダー（SpeakerDock/Placeholder, LocakDock/Placeholder）

タブレットの初期位置にはプレースホルダー（Placeholder）が配置されています。
タブレットが初期位置から離れるとプレースホルダーが表示され、プレースホルダーをUseするとタブレットの位置を初期位置にリセットできます。

プレースホルダーは次の手順で複製して複数設置できます。

1. ワールド上に設置したSlidenプレハブの中で「SpeakerDock」や「LocalDock」の中にある「Placeholder」オブジェクトを選択し、右クリックメニューから「Duplicate」でPlaceholderを複製する
2. 複製された「Placeholder (1)」を好きな位置に移動する

## お問合せ先

* Discord Slidenコミュニティサーバ: https://discord.gg/YSsjKdujHa
* DiscordID: [はるまきちくわ#8199](https://discordapp.com/users/622442050813427713)
* Misskey: https://misskey.niri.la/@hlmk3vr

## ライセンス

* Icons - Apache License V2.0
* Sounds - CC BY 4.0 （OtoLogic様の素材を使用しています）
* それ以外のアセット一式 - VN3ライセンス（vn3license_ja.pdfを参照）
