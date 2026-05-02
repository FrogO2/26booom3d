# FPS Controller Worklog (2026-04-25)

## 已完成范围

已在 Assets/Scripts/FirstPersonController.cs 内实现 Phase 1 到 Phase 2 的可运行控制器框架，核心包括：

1. 基础第一人称能力
- 视角控制（水平旋转 + 俯仰限制）
- 基础移动（走路、冲刺、蹲伏）
- 重力、地面吸附、跳跃
- 空中控制

2. 手感辅助机制
- 土狼时间（coyote time）
- 跳跃缓冲（jump buffer）
- 额外空中跳跃（默认 1 次）

3. 跑酷能力（Phase 2）
- 滑铲（冲刺 + 蹲伏 + 前进触发）
- 墙跑（左右墙检测，沿墙方向移动）
- 墙跳（墙跑中跳跃，带水平和垂直弹出力）

4. 姿态与头部空间处理
- 站立/蹲伏高度平滑插值
- 顶头空间检测（防止无法站立时强行起身）

5. 相机反馈
- 冲刺/滑铲 FOV 变化
- 墙跑倾斜（左右侧倾）

6. 调试可视化
- Gizmos 显示地面探测与左右墙体探测线

## 输入接入方式

当前脚本直接通过 InputActionAsset 绑定 Player Action Map 的以下动作：
- Move
- Look
- Jump
- Sprint
- Crouch

说明：项目中未使用自动生成的 InputSystem C# wrapper，而是运行时查找动作映射。

## 关键配置要求（场景内）

1. 玩家层级建议
- Player（挂载 CharacterController + FirstPersonController）
- CameraRoot（Player 子物体）
- Main Camera（CameraRoot 子物体）

2. Inspector 必填引用
- playerCamera 指向 Main Camera
- cameraRoot 指向 CameraRoot
- inputActions 指向 Assets/InputSystem_Actions.inputactions
- actionMapName 保持 Player

3. 检测层设置
- environmentMask 需要包含地面、墙面、天花板等参与碰撞的层
- 未包含时会直接导致地面检测、墙跑检测或站立顶头检测失效

## 当前代码中的已知状态

1. 用户新增了调试日志行
- Update 中有一行状态日志（当前为注释）
- HandleSlide 中有 WantsCrouch 的实时日志（当前启用）

2. 若后续出现控制台刷屏，可优先关闭 HandleSlide 内的 Debug.Log。

## 交接建议（下次继续时）

1. 优先做稳定性回归
- 斜坡、台阶、窄通道、低天花板、连续跳跃、贴墙进出状态

2. Phase 2 可继续增强项
- 翻越/攀爬（vault/mantle）
- 更细的墙跑进入角度限制
- 落地缓冲动画曲线或镜头冲击

3. 结构优化建议
- 将输入、环境检测、移动状态拆分为独立组件或 partial 文件，降低单脚本复杂度

## 快速复现清单

1. 勾选环境层并确认场景碰撞体完整
2. 绑定输入资产和相机引用
3. 运行后验证：走、冲刺、蹲伏、滑铲、跳跃、二段跳、墙跑、墙跳
4. 观察 Scene Gizmos，确认地面探测球和左右墙探测线位置合理
