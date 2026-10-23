import pathlib, subprocess, sys
root = pathlib.Path(__file__).parent
sys.path.insert(0, str(root / '.deps'))
import imageio_ffmpeg
ff = imageio_ffmpeg.get_ffmpeg_exe()
output = root / 'shot01_v2_1080p_48fps.mp4'
# Interpolate the original moving frames before scaling, avoiding the repeated
# frames in the 24 fps delivery. Keep the same timing and camera easing.
vf = ("setpts=PTS*1.185185185,tpad=stop_mode=clone:stop_duration=0.3,"
      "minterpolate=fps=48:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1,"
      "scale=1920:1108:flags=lanczos,crop=1920:1080,setsar=1,"
      "zoompan=z='1+0.16*(1-pow(1-min(on/96,1),3))':"
      "x='(iw-iw/zoom)*0.65':y='(ih-ih/zoom)*0.35':d=1:s=1920x1080:fps=48,tpad=stop_mode=clone:stop_duration=1")
subprocess.run([ff, '-hide_banner', '-y', '-i', str(root/'shot01_v2_raw.mp4'),
 '-i', str(root/'shot01_v2_1080p_24fps.mp4'), '-map', '0:v:0', '-map', '1:a:0',
 '-vf', vf, '-frames:v', '288', '-t', '6', '-c:v', 'libx264', '-crf', '18',
 '-pix_fmt', 'yuv420p', '-c:a', 'copy', '-movflags', '+faststart', str(output)], check=True)
subprocess.run([ff, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(output),
 '-vf', 'select=eq(n\\,287)', '-frames:v', '1', '-update', '1',
 str(root/'shot01_v2_48fps_final_frame.png')], check=True)
print('VERIFIED frames, seconds:', imageio_ffmpeg.count_frames_and_secs(str(output)))
reader = imageio_ffmpeg.read_frames(str(output))
print('METADATA:', next(reader))
reader.close()

