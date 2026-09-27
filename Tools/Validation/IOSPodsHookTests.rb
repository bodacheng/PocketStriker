require 'logger'
require 'cocoapods'
require 'ostruct'

podfile = Pod::Podfile.from_file(Pathname.new(ARGV.fetch(0)))
settings = ['12.0', '15.0', '15.10', '16.0', nil, '$(inherited)'].map do |version|
  OpenStruct.new(build_settings: { 'IPHONEOS_DEPLOYMENT_TARGET' => version })
end
installer = OpenStruct.new(pods_project: OpenStruct.new(targets: [OpenStruct.new(build_configurations: settings)]))
podfile.post_install!(installer)
actual = settings.map { |configuration| configuration.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] }
expected = ['15.0', '15.0', '15.10', '16.0', nil, '$(inherited)']
raise "Version comparison changed unexpected settings: #{actual}" unless actual == expected
podfile.post_install!(installer)
raise 'Hook is not idempotent' unless settings.map { |c| c.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] } == expected

# Load the real generated Pods project into memory; do not save or modify the export.
project = Xcodeproj::Project.open(ARGV.fetch(1))
configurations = project.targets.flat_map(&:build_configurations)
before = configurations.map { |config| config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] }
minimum = Gem::Version.new('15.0')
podfile.post_install!(OpenStruct.new(pods_project: project))
configurations.zip(before).each do |config, previous|
  next unless previous.to_s.match?(/\A\d+(?:\.\d+)*\z/)
  expected_version = Gem::Version.new(previous) < minimum ? '15.0' : previous
  raise "Wrong target for #{config.name}: #{config.build_settings}" unless config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] == expected_version
end
raised = before.count { |version| version && Gem::Version.new(version) < minimum }
puts "PASS: Ruby hook preserves equal/higher/inherited targets; #{configurations.size} real Pods configurations checked (#{raised} raised to 15.0 in memory)"
