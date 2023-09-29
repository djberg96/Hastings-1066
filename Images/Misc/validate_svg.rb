require 'nokogiri'
file = ARGV.shift
file = file.chomp if file

if file
  doc = Nokogiri::XML.parse(File.open(file))

  if !doc.errors.empty?
    p f
    p doc.errors
  else
    puts "File '#{file}' looks ok!"
  end
else
  Dir["*.svg"].each do |f|
    doc = Nokogiri::XML.parse(File.open(f))

    if !doc.errors.empty?
      p f
      p doc.errors
    end
  end
end
