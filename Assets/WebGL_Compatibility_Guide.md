# WebGL 渲染管线修复 - 完成总结

## 已完成的修改清单

### ✅ 1. FrozenProjectorManager.cs
**文件路径**: `Assets/Scripts/FrozenProjectorManager.cs`

**主要改动**:
- 使用 `#if UNITY_WEBGL` 条件编译分离 PC 和 WebGL 实现
- **Texture2DArray → Texture2D[]**: WebGL 使用单独的 Texture2D 数组 (16 个) 替代 Texture2DArray
- **TextureFormat.RGBAHalf → TextureFormat.RGBA32**: 使用 WebGL 完全兼容的纹理格式
- **移除 AsyncGPUReadback**: WebGL 不支持此 API，HD 深度捕获功能被禁用
- **添加 CaptureProjectorVisibilityWebGL**: WebGL 专用的 CPU Raycast 深度捕获方法
- **ApplySharedVisibilityData**: WebGL 设置单独的纹理属性 `_ProjectorVisibilityAtlas0` 到 `_ProjectorVisibilityAtlas15`

### ✅ 2. BloodRevealRendererFeature.cs
**文件路径**: `Assets/Scripts/BloodRevealRendererFeature.cs`

**改动**: Shader 名称条件编译
```csharp
#if UNITY_WEBGL
    private const string DefaultShaderName = "Hidden/MyProject/BloodRevealMask_WebGL";
#else
    private const string DefaultShaderName = "Hidden/MyProject/BloodRevealMask";
#endif
```

### ✅ 3. BloodFxRendererFeature.cs
**文件路径**: `Assets/Scripts/BloodFxRendererFeature.cs`

**改动**: Shader 名称条件编译
```csharp
#if UNITY_WEBGL
    private const string DefaultShaderName = "Hidden/MyProject/BloodProjectorFx_WebGL";
#else
    private const string DefaultShaderName = "Hidden/MyProject/BloodProjectorFx";
#endif
```

### ✅ 4. BloodObjectMaskRendererFeature.cs
**文件路径**: `Assets/Scripts/BloodObjectMaskRendererFeature.cs`

**主要改动**:
- 添加 `BloodObjectMaskWebGLPass` 类（使用 CommandBuffer 而非 RenderGraph）
- `Create()`, `AddRenderPasses()`, `Dispose()` 方法添加平台分支
- WebGL 使用 `DrawRenderers` API 而非 `DrawRendererList`

### ✅ 5. BloodRevealMask_WebGL.shader
**文件路径**: `Assets/Arts/Shaders/BloodRevealMask_WebGL.shader`

**主要改动**:
- 使用单独的 2D 纹理声明替代 `TEXTURE2D_ARRAY`
- 添加 `SampleVisibilityAtlas` 辅助函数根据索引选择纹理
- 修改深度比较逻辑适配 byte 编码的深度值
- 添加 `#pragma target 3.0` 确保 WebGL 2.0 兼容

### ✅ 6. BloodProjectorFx_WebGL.shader
**文件路径**: `Assets/Arts/Shaders/BloodProjectorFx_WebGL.shader`

**主要改动**:
- 同 BloodRevealMask_WebGL.shader 的纹理数组处理
- 添加 `EvaluateProjector` 函数的 WebGL 兼容版本
- 柔化范围 `softFadeRange = 0.15` 避免深度边界锯齿

---

## 需要手动完成的步骤

### 1. 创建 WebGL 专用的 Renderer Data

在 Unity 编辑器中操作：

1. 打开 Project 窗口，定位到 `Assets/Settings/`
2. 右键 `PC_Renderer.asset` → **Copy**
3. 右键空白处 → **Paste**
4. 重命名新文件为 `WebGL_Renderer.asset`
5. 双击打开，修改以下设置：
   - **Renderer Features**:
     - 禁用 `DecalRendererFeature` (WebGL 不支持)
     - 确保 `BloodObjectMaskRendererFeature` 存在
     - 确保 `BloodRevealRendererFeature` 存在（会自动使用 WebGL shader）
     - 确保 `BloodFxRendererFeature` 存在（会自动使用 WebGL shader）
   - **Layer Masks**:
     - Prepass Layer Mask: 保持默认
     - Opaque Layer Mask: 保持默认
     - Transparent Layer Mask: 保持默认

### 2. 创建 WebGL 专用的 RP Asset

在 Unity 编辑器中操作：

1. 打开 Project 窗口，定位到 `Assets/Settings/`
2. 右键 `PC_RPAsset.asset` → **Copy**
3. 右键空白处 → **Paste**
4. 重命名新文件为 `WebGL_RPAsset.asset`
5. 双击打开，修改以下设置：
   ```
   m_RequireDepthTexture: 1
   m_RequireOpaqueTexture: 0
   m_OpaqueDownsampling: 1
   m_MSAA: 1
   m_RenderScale: 1
   m_MainLightRenderingMode: 1 (Active)
   m_AdditionalLightsRenderingMode: 1 (Active)
   m_ShadowDistance: 50
   m_ShadowCascadeCount: 1 (减少 WebGL 负担)
   ```

### 3. 配置 WebGL 构建设置

1. **File > Build Settings**
2. 选择 **WebGL** 平台
3. 点击 **Switch Platform**（如果尚未切换）
4. 点击 **Player Settings**
5. 修改以下设置：
   ```
   Other Settings:
   - Graphics API: OpenGLES3 (确保选中，移除其他)
   - Color Space: Linear
   - Scripting Backend: IL2CPP
   - Exceptions: Explicit Thrown
   - Code Optimization: Optimize Size
   - Debugging: unchecked (发布时)
   ```

### 4. 配置场景使用的渲染管线

在每个需要支持 WebGL 的场景中：

1. 打开场景文件
2. 选择 **Main Camera**
3. 在 Inspector 中找到 **Universal Render Pipeline Asset**
4. 暂时留空（构建时会自动使用 WebGL_RPAsset）

或者在 **Project Settings > Graphics** 中设置：
- **Scriptable Render Pipeline Settings**: 指向 `WebGL_RPAsset`

---

## WebGL 功能对比表

| 功能 | PC (Windows/Mac/Linux) | WebGL (OpenGL ES 3.0) | 备注 |
|------|------------------------|----------------------|------|
| **Texture2DArray** | ✅ 支持 | ❌ 不支持 | WebGL 使用 16 个单独 Texture2D |
| **AsyncGPUReadback** | ✅ 支持 | ❌ 不支持 | WebGL 完全不支持此 API |
| **HD 深度捕获** | ✅ 异步 GPU 捕获 | ⚠️ CPU Raycast | WebGL 降级为 CPU 实现 |
| **DecalRenderer** | ✅ 支持 | ❌ 不支持 | OpenGL ES 不支持 |
| **RenderGraph** | ✅ 完整支持 | ⚠️ 部分支持 | WebGL 使用 CommandBuffer 回退 |
| **RGBAHalf 纹理** | ✅ 支持 | ❌ 不支持 | WebGL 使用 RGBA32 |
| **R16G16B16A16_SFloat** | ✅ 支持 | ❌ 不支持 | WebGL 使用 D32_SFloat_S8_UInt |
| **最大 Projectors** | 16 | 16 | 保持不变 |
| **捕获分辨率** | 推荐 256-512 | 推荐 64-128 | WebGL 建议降低分辨率 |
| **DrawRendererList** | ✅ 支持 | ❌ 不支持 | WebGL 使用 DrawRenderers |

---

## 性能优化建议

### 1. 降低捕获分辨率
```csharp
// 在调用 FrozenProjectorManager.AddProjector 时
int captureResolution = 128; // PC 可用 256-512，WebGL 建议 64-128
```

### 2. 减少最大 Projector 数量
```csharp
// 如果不需要 16 个，可以减少
FrozenProjectorManager.SetMaxRetainedProjectors(8);
```

### 3. 禁用不必要的后处理
在 `WebGL_RPAsset` 中：
- 禁用 SSAO（Screen Space Ambient Occlusion）
- 降低阴影质量

### 4. 使用 IL2CPP 代码后端
- 在 Player Settings 中选择 IL2CPP
- 比 Mono 快 20-30%

---

## 测试步骤

### 1. 本地测试
```
1. 在 Unity 中切换到 WebGL 平台
2. 打开一个测试场景
3. 点击 Play 测试（使用 WebGL 模拟）
```

### 2. 构建测试
```
1. File > Build Settings > WebGL > Build
2. 使用本地 Web 服务器托管构建文件
   - Python: python -m http.server 8000
   - Node: npx http-server
3. 浏览器访问 http://localhost:8000
```

### 3. 浏览器调试
**Firefox**:
```
about:debugging → 检查 WebGL 状态
Browser Console → 查看错误
```

**Chrome**:
```
chrome://inspect → 检查 WebGL 上下文
DevTools Console → 查看错误
```

### 4. 查看控制台错误
关键错误类型：
- `GL_INVALID_ENUM`: 纹理格式不支持
- `GL_INVALID_OPERATION`: Shader 编译失败
- `Out of video memory`: 纹理过大

---

## 常见问题解决

### Q1: 构建后黑屏/无渲染
**可能原因**:
- Shader 编译错误
- 纹理格式不支持

**解决方法**:
```
1. 打开浏览器控制台查看错误
2. 检查 Shader 是否使用了 WebGL 不支持的语法
3. 确认 WebGL 2.0 (OpenGL ES 3.0) 已启用
```

### Q2: 击杀敌人后视野无变化
**可能原因**:
- `FrozenProjectorManager.AddProjector` 返回 -1
- 纹理未正确设置到 Shader

**调试步骤**:
```csharp
// 在代码中添加调试
int projectorId = FrozenProjectorManager.AddProjector(...);
Debug.Log($"Projector ID: {projectorId}");

// 检查纹理是否正确
var mat = yourRendererMaterial;
for (int i = 0; i < 16; i++)
{
    var tex = mat.GetTexture($"_ProjectorVisibilityAtlas{i}");
    Debug.Log($"Atlas {i}: {tex != null}");
}
```

### Q3: 性能过低（< 30 FPS）
**优化建议**:
```
1. 降低 captureResolution 到 64
2. 减少 Shadow Cascade 到 1
3. 禁用 SSAO
4. 降低阴影距离
5. 减少最大 Projector 数量到 8
```

### Q4: Decal 不显示
**说明**: 这是预期行为，WebGL 不支持 DecalRendererFeature。

**替代方案**:
- 使用 Projector 组件（旧版但兼容 WebGL）
- 使用自定义 Shader 模拟

### Q5: Shader 编译错误 "unknown type 'Texture2DArray'"
**原因**: 使用了纹理数组语法

**解决**: 确保 WebGL 构建使用 `*_WebGL.shader` 变体

---

## 构建检查清单

在发布 WebGL 构建前，确认以下项目：

- [ ] 已创建 `WebGL_RPAsset.asset`
- [ ] 已创建 `WebGL_Renderer.asset`
- [ ] 场景中相机使用正确的 Renderer
- [ ] Player Settings 中 Graphics API 为 OpenGLES3
- [ ] Scripting Backend 为 IL2CPP
- [ ] 测试构建在 Firefox/Chrome 中运行正常
- [ ] 浏览器控制台无错误
- [ ] 击杀敌人功能正常工作
- [ ] 视野揭示效果正常
- [ ] 血液特效正常
- [ ] FPS 达到目标（推荐 60 FPS）

---

## 文件清单

### 已修改的文件
```
Assets/Scripts/FrozenProjectorManager.cs
Assets/Scripts/BloodRevealRendererFeature.cs
Assets/Scripts/BloodFxRendererFeature.cs
Assets/Scripts/BloodObjectMaskRendererFeature.cs
```

### 新增的文件
```
Assets/Arts/Shaders/BloodRevealMask_WebGL.shader
Assets/Arts/Shaders/BloodProjectorFx_WebGL.shader
Assets/WebGL_Compatibility_Guide.md (本文档)
```

### 需要手动创建的文件
```
Assets/Settings/WebGL_RPAsset.asset
Assets/Settings/WebGL_Renderer.asset
```

---

## 联系与支持

如果在 WebGL 构建过程中遇到问题，请检查：
1. Unity 版本（推荐 2022 LTS 或更高）
2. WebGL 模板设置
3. 浏览器 WebGL 支持情况
4. 控制台错误日志
