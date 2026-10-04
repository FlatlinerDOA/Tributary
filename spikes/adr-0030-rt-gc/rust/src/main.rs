use std::time::Instant;

const BLOCK: usize = 64;
const RATE: f64 = 48000.0;

struct Engine {
    tracks: usize,
    stages: usize,
    state: Vec<f32>,
    phase: Vec<f32>,
    inc: Vec<f32>,
    l: [f32; BLOCK],
    r: [f32; BLOCK],
}

impl Engine {
    fn new(tracks: usize) -> Self {
        let mut e = Engine {
            tracks,
            stages: 1,
            state: vec![],
            phase: vec![0.0; tracks],
            inc: (0..tracks).map(|i| 2.0 * std::f32::consts::PI * (110.0 + 7.0 * i as f32) / 48000.0).collect(),
            l: [0.0; BLOCK],
            r: [0.0; BLOCK],
        };
        e.set_stages(1);
        e
    }
    fn set_stages(&mut self, s: usize) {
        self.stages = s;
        self.state = vec![0.0; self.tracks * s * 2];
    }
    fn calibrate(&mut self, target_us: f64) -> usize {
        for s in 1..4096 {
            self.set_stages(s);
            for _ in 0..2000 { self.process(); }
            let t0 = Instant::now();
            for _ in 0..2000 { self.process(); }
            let us = t0.elapsed().as_secs_f64() * 1e6 / 2000.0;
            if us >= target_us { return s; }
        }
        self.stages
    }
    fn process(&mut self) {
        self.l.fill(0.0);
        self.r.fill(0.0);
        const B0: f32 = 0.2; const B1: f32 = 0.4; const B2: f32 = 0.2; const A1: f32 = -0.3; const A2: f32 = 0.1;
        for t in 0..self.tracks {
            let mut ph = self.phase[t];
            let inc = self.inc[t];
            let pan = if t & 1 == 0 { 0.6 } else { 0.4 };
            for i in 0..BLOCK {
                let x2 = ph * ph;
                let mut x = ph * (1.0 - x2 * (0.16605 - x2 * 0.00761));
                ph += inc;
                if ph > std::f32::consts::PI { ph -= 2.0 * std::f32::consts::PI; }
                for s in 0..self.stages {
                    let k = (t * self.stages + s) * 2;
                    let y = B0 * x + self.state[k];
                    self.state[k] = B1 * x - A1 * y + self.state[k + 1];
                    self.state[k + 1] = B2 * x - A2 * y;
                    x = y;
                }
                self.l[i] += x * pan;
                self.r[i] += x * (1.0 - pan);
            }
            self.phase[t] = ph;
        }
    }
}

fn arg(a: &[String], k: &str, d: &str) -> String {
    a.iter().position(|x| x == &format!("--{k}")).map(|i| a[i + 1].clone()).unwrap_or(d.into())
}

fn main() {
    let a: Vec<String> = std::env::args().collect();
    let seconds: f64 = arg(&a, "seconds", "60").parse().unwrap();
    let load: f64 = arg(&a, "load", "0.7").parse().unwrap();
    let tracks: usize = arg(&a, "tracks", "200").parse().unwrap();
    let period_ns = BLOCK as f64 / RATE * 1e9;
    let period_us = period_ns / 1e3;

    let mut e = Engine::new(tracks);
    let stages = e.calibrate(load * period_us);
    println!("runtime=rust tracks={tracks} stages={stages}");

    let mut hist = vec![0u64; 20_000];
    let (mut xruns, mut blocks, mut max_late, mut max_dur) = (0u64, 0u64, 0f64, 0f64);
    let start = Instant::now();
    let mut n = 0u64;
    loop {
        let s = n as f64 * period_ns;
        if s >= seconds * 1e9 { break; }
        while (start.elapsed().as_nanos() as f64) < s { std::hint::spin_loop(); }
        let t0 = start.elapsed().as_nanos() as f64;
        e.process();
        let t1 = start.elapsed().as_nanos() as f64;
        let late = t1 - (n + 1) as f64 * period_ns;
        if late > 0.0 { xruns += 1; max_late = max_late.max(late); }
        let dur = t1 - t0;
        max_dur = max_dur.max(dur);
        hist[((dur / 1e3) as usize).min(19_999)] += 1;
        blocks += 1;
        n += 1;
    }
    let total: u64 = hist.iter().sum();
    let pct = |p: f64| { let t = (total as f64 * p).ceil() as u64; let mut acc = 0; for (i, c) in hist.iter().enumerate() { acc += c; if acc >= t { return i; } } 19_999 };
    let mean = hist.iter().enumerate().map(|(i, c)| i as f64 * *c as f64).sum::<f64>() / total as f64;
    println!("blocks={blocks} xruns={xruns} max_late_us={:.0} max_callback_us={:.0} p50_us={} p99_us={} p99.99_us={} mean_load={:.1}%", max_late / 1e3, max_dur / 1e3, pct(0.5), pct(0.99), pct(0.9999), mean / period_us * 100.0);
}
