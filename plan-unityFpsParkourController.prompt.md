## Plan: Unity FPS Parkour Controller

为当前空白的 FirstPersonController 搭建一个分层的一人称跑酷控制器方案。推荐先实现可稳定调参的基础移动内核（视角、地面移动、重力、跳跃、冲刺、蹲伏），再在此基础上叠加跑酷能力（滑铲、二段跳、墙跑、翻越/攀爬、空中控制等），并把输入、状态、环境检测和参数解耦，避免后续功能互相打架。

**Steps**
1. Phase 1 - 定义 MVP 范围：以 CharacterController 为核心，在 FirstPersonController 中先落地 Look、Move、Jump、Sprint、Crouch，对接现有 Input System Player 动作映射。
2. Phase 1 - 建立控制器内部结构：拆成输入采样、相机旋转、水平移动、垂直速度、环境检测、状态判定六个逻辑段；即使先放在单文件里，也要按方法边界分层，便于后续再拆文件。
3. Phase 1 - 实现基础移动参数：步行速度、冲刺速度、蹲伏速度、加速度、减速度、空中控制、重力、跳跃高度、终端下落速度、鼠标灵敏度、站立/蹲伏高度、头部相机偏移。
4. Phase 1 - 设计基础状态流转：Grounded、Jumping、Falling、Sprinting、Crouching 允许并行组合，保持“位移能力”和“姿态能力”分离；避免用单个 enum 把所有状态写死。*depends on 2*
5. Phase 2 - 加入跑酷检测层：前向、侧向、顶部射线或胶囊检测用于判断可滑铲空间、墙面、可翻越边缘、顶头障碍；统一封装为环境查询方法。*depends on 2*
6. Phase 2 - 扩展跑酷能力：优先顺序建议为滑铲、二段跳/缓冲跳、土狼时间、墙跑、翻越/攀爬；每个能力单独定义进入条件、持续条件、退出条件、速度修正。*depends on 5*
7. Phase 2 - 处理相机反馈：FOV 变化、头部摆动、落地镜头缓冲、滑铲压低视角、墙跑倾斜；这些都应作为表现层，避免直接驱动核心物理。*parallel with 6*
8. Phase 3 - 参数资产化：若功能稳定，再将移动参数抽到 ScriptableObject 或序列化配置组，便于快速调手感和做不同角色模板。*depends on 1-7*
9. Phase 3 - 补充调试与验证：添加 Gizmos、调试日志开关、地面/墙体法线可视化，以及针对关键状态切换的 Play Mode 验证。*parallel with 8*

**Relevant files**
- `c:/Users/Gingko/unity projects/My project/Assets/Scripts/FirstPersonController.cs` — 当前唯一控制器入口；建议作为 MVP 主实现文件，先按方法和字段分区组织。
- `c:/Users/Gingko/unity projects/My project/Assets/InputSystem_Actions.inputactions` — 已存在的输入动作资源；可直接复用 Move、Look、Jump、Sprint、Crouch。

**Verification**
1. 在 Unity 中将 FirstPersonController 挂到玩家对象，并确认 CharacterController、Camera 引用、Input System 绑定生效。
2. 验证基础手感：站立移动、视角旋转、跳跃落地、冲刺、蹲伏、空中控制是否符合预期。
3. 验证边界行为：斜坡、台阶、低矮通道、连续跳跃、落地瞬间输入缓冲、贴墙移动是否稳定。
4. 对每个跑酷能力单独测试进入/退出条件，确认不会和 Sprint、Crouch、Jump 状态冲突。
5. 用不同参数组合回归测试，确保高速度下不会出现穿模、卡墙、无法站起等问题。

**Decisions**
- 核心驱动建议使用 CharacterController，而不是刚开始就用 Rigidbody：更容易先把 FPS 跑酷手感做出来。
- 输入层直接复用现有 Input System 动作资源，不重新设计输入映射。
- 初版优先保证可调参与稳定性，不把所有跑酷能力一次性塞进首个版本。
- 本计划包含功能设计和落地顺序，不包含美术动画、音效、武器系统和联网同步。

**Further Considerations**
1. 墙跑方向判定建议基于墙体法线和玩家前向的叉乘，而不是只看左右输入，这样更稳定。
2. 翻越/攀爬建议等基础移动稳定后再做，否则容易与 CharacterController 的阶梯和碰撞逻辑互相影响。
3. 如果目标更偏竞技高速移动，可提前把“保速度”与“空中转向”作为核心调参项；如果偏沉浸探索，则优先做镜头反馈和落地缓冲。
