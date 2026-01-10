using LiteMonitor.src.Core;

namespace LiteMonitor
{
    /// <summary>
    /// 定义该指标项的渲染风格
    /// </summary>
    public enum MetricRenderStyle
    {
        StandardBar, // 标准：左标签 + 右数值 + 底部进度条 (CPU/MEM/GPU)
        TwoColumn    // 双列：居中标签 + 居中数值 (NET/DISK)
    }

    public class MetricItem
    {
        private string _key = "";
        
        // [保留优化] 强制驻留字符串
        public string Key 
        { 
            get => _key;
            set => _key = UIUtils.Intern(value); 
        }

        private string _label = "";
        public string Label 
        {
            get => _label;
            set => _label = UIUtils.Intern(value);
        }
        
        // [保留优化] 缓存短标签
        private string _shortLabel = "";
        public string ShortLabel 
        {
            get => _shortLabel;
            set => _shortLabel = UIUtils.Intern(value);
        }
        
        public float? Value { get; set; } = null;
        public float DisplayValue { get; set; } = 0f;

        // =============================
        // [保留优化] 缓存字段
        // =============================
        private float _cachedDisplayValue = -99999f; // 上一次格式化时的数值
        private string _cachedNormalText = "";       // 缓存竖屏文本
        private string _cachedHorizontalText = "";   // 缓存横屏/任务栏文本

        /// <summary>
        /// 获取格式化后的文本（带缓存机制）
        /// </summary>
        /// <param name="isHorizontal">是否为横屏/任务栏模式（需要极简格式）</param>
        public string GetFormattedText(bool isHorizontal)
        {
            // [保留优化] 阈值检查：防止浮点数微小抖动导致重绘
            if (Math.Abs(DisplayValue - _cachedDisplayValue) > 0.05f)
            {
                _cachedDisplayValue = DisplayValue;

                // 1. 重新生成基础字符串
                _cachedNormalText = UIUtils.FormatValue(Key, DisplayValue);

                // 2. 重新生成横屏字符串
                // 配合 UIUtils.FormatHorizontalValue 的去正则优化，这里效率极高
                _cachedHorizontalText = UIUtils.FormatHorizontalValue(_cachedNormalText);
            }

            // 返回对应模式的缓存
            return isHorizontal ? _cachedHorizontalText : _cachedNormalText;
        }

        // =============================
        // 布局数据 (由 UILayout 计算填充)
        // =============================
        public MetricRenderStyle Style { get; set; } = MetricRenderStyle.StandardBar;
        public Rectangle Bounds { get; set; } = Rectangle.Empty;

        public Rectangle LabelRect;   
        public Rectangle ValueRect;   
        public Rectangle BarRect;     
        public Rectangle BackRect;    

        /// <summary>
        /// 平滑更新显示值
        /// </summary>
        public void TickSmooth(double speed)
        {
            if (!Value.HasValue) return;
            float target = Value.Value;
            float diff = Math.Abs(target - DisplayValue);

            // 忽略极小的变化，防止动画抖动
            if (diff < 0.05f) return;

            // 距离过大或速度过快时直接跳转
            if (diff > 15f || speed >= 0.9)
                DisplayValue = target;
            else
                DisplayValue += (float)((target - DisplayValue) * speed);
        }
    }
}