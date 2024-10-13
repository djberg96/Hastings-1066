Dir["*.svg"].each do |file|
  puts "File: #{file}"
  base = File.basename(file, '.svg')
  cmd = "inkscape -d 1200 -o #{base}.png #{file}"
  system(cmd)
end
