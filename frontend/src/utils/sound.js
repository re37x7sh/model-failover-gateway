/**
 * 使用 Web Audio API 原生合成清脆悦耳的完成提示音（Ding-Dong~）
 * 0 依赖，无需加载外部音频文件，高保真零延迟
 */
export function playChimeSound() {
  try {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;

    const ctx = new AudioContext();
    const now = ctx.currentTime;

    // 播放双音符大调和弦（D5 587.33Hz -> A5 880Hz）
    playTone(ctx, 587.33, now, 0.45, 0.25);
    playTone(ctx, 880.00, now + 0.12, 0.65, 0.3);
  } catch (err) {
    console.warn('Web Audio 提示音播放受限:', err);
  }
}

function playTone(ctx, freq, startTime, duration, maxGain) {
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();

  // 正弦波 + 轻微泛音衰减，音色类似清脆的水滴或编钟
  osc.type = 'sine';
  osc.frequency.setValueAtTime(freq, startTime);

  // 指数衰减包络
  gain.gain.setValueAtTime(0.001, startTime);
  gain.gain.exponentialRampToValueAtTime(maxGain, startTime + 0.02);
  gain.gain.exponentialRampToValueAtTime(0.0001, startTime + duration);

  osc.connect(gain);
  gain.connect(ctx.destination);

  osc.start(startTime);
  osc.stop(startTime + duration);
}
