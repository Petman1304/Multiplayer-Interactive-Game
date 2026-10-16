"""Generate original integration audio using only Python's standard library.
Run from any directory. No downloaded recordings, packages, or runtime synthesis.
"""
from pathlib import Path
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Resources/GetawayAudio'
RATE = 22050
TAU = math.tau

def write(name, samples):
    peak = max(abs(x) for x in samples) or 1
    gain = min(1, .88 / peak)
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as output:
        output.setparams((1, 2, RATE, len(samples), 'NONE', 'not compressed'))
        output.writeframes(b''.join(struct.pack('<h', round(max(-1, min(1, x * gain)) * 32767)) for x in samples))

def tone(f, t):
    return math.sin(TAU * f * t)

def effect(name, duration, fn):
    rng = random.Random(710 + sum(map(ord, name)))
    count = round(duration * RATE)
    write(name, [fn(i / RATE, rng.uniform(-1, 1)) * min(1, i / 32, (count-1-i) / 64) for i in range(count)])

def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    effect('shot', .24, lambda t,n: (.65*n*math.exp(-t*42)+.7*tone(78,t)*math.exp(-t*23)+.3*tone(160,t)*math.exp(-t*48)))
    effect('hit', .13, lambda t,n: (.55*n+.25*tone(1900,t))*math.exp(-35*t))
    effect('impact', .48, lambda t,n: (.8*n*math.exp(-t*13)+.55*tone(55,t)*math.exp(-t*9)+.18*tone(183,t)*math.exp(-t*18)))
    effect('pass', .65, lambda t,n: n*.3*math.sin(math.pi*t/.65)**2)
    effect('reload', .48, lambda t,n: n*(.45*math.exp(-45*t)+.6*math.exp(-90*abs(t-.28)))+.12*tone(420,t)*math.exp(-18*t))
    effect('ready', .16, lambda t,n: (.5*n+.3*tone(780,t))*math.exp(-32*t))
    effect('pickup', .45, lambda t,n: .42*tone(523 if t<.14 else 659 if t<.28 else 784,t)*math.sin(math.pi*t/.45)**2)
    effect('countdown', .16, lambda t,n: .42*tone(660,t)*math.sin(math.pi*t/.16)**2)
    effect('start', .42, lambda t,n: (.28*tone(880,t)+.2*tone(1320,t))*math.sin(math.pi*t/.42)**2)
    effect('end', 1.2, lambda t,n: (.3*tone(220,t)+.25*tone(164.81,t)+.2*tone(130.81,t))*math.sin(math.pi*t/1.2)**2)
    # Integer-period oscillators and a periodic noise table keep loop seams quiet.
    rng=random.Random(928)
    noise=[rng.uniform(-1,1) for _ in range(RATE)]
    write('engine', [.28*tone(48,i/RATE)+.16*tone(96,i/RATE)+.09*tone(144,i/RATE)+.05*noise[i] for i in range(RATE)])
    # Circular lowpass noise, crossfaded to itself for a quiet wind-loop boundary.
    smooth=[]; low=0
    for n in noise*2:
        low=low*.72+n*.28
        smooth.append(low*.45)
    wind=smooth[RATE:]
    for i in range(512):
        blend=i/511
        wind[i]=wind[-512+i]*(1-blend)+wind[i]*blend
    wind[-1]=wind[0]
    write('wind', wind)
    # One complete sinusoidal frequency sweep: phase and frequency match at seam.
    write('siren', [.36*math.sin(TAU*650*(i/RATE)+90*(1-math.cos(TAU*(i/RATE)))) for i in range(RATE)])
    # Eight bars, 110 BPM, D minor / Bb / F / C. Loop is integer sample length.
    beat=60/110; length=round(32*beat*RATE); samples=[]; rng=random.Random(402)
    chords=[(146.8324,174.6141,220),(116.5409,146.8324,174.6141),(130.8128,174.6141,220),(130.8128,164.8138,195.9977)]
    for i in range(length):
        t=i/RATE; b=t/beat; bar=int(b//4); chord=chords[(bar//2)%4]; q=b%1; h=b%.5
        # Reset phases at note boundaries behind gentle envelopes.
        age=q*beat; eighth=h*beat; bass=chord[0]/2
        kick=.48*math.sin(TAU*(48*age+4*(1-math.exp(-35*age))))*math.exp(-age*17)
        n=rng.uniform(-1,1)
        snare=.17*n*math.exp(-age*24) if int(b)%4 in (1,3) else 0
        hat=.055*n*math.exp(-eighth*70)
        bassline=.21*(tone(bass,age)+.18*tone(bass*2,age))*min(1,age*120)*math.exp(-age*4)
        arp=chord[int(b*2)%3]*2
        pluck=.075*(tone(arp,eighth)+.15*tone(arp*2,eighth))*min(1,eighth*200)*math.exp(-eighth*10)
        chordAge=(b%4)*beat
        pad=sum(tone(f,chordAge) for f in chord)*.035*min(1,chordAge*5)*min(1,(4*beat-chordAge)*5)
        # Fade only outer 15ms so repeat remains rhythmically continuous.
        edge=min(1,i/(RATE*.015),(length-1-i)/(RATE*.015))
        samples.append((kick+snare+hat+bassline+pluck+pad)*edge)
    write('downtown', samples)
    print('Generated 14 original mono clips:', sum(p.stat().st_size for p in ROOT.glob('*.wav')), 'bytes')

if __name__ == '__main__':
    main()
