mixin 'check webspace properties' do |webspace|
  assert webspace.data.platform.to be 'linux'
  assert webspace.owner.username.to be "o#{webspace.data.webspaceId}"
end