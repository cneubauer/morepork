mixin 'check webspace properties' do |webspace|
  assert webspace.data.platform.to be 'linux'
  assert webspace.owner.username.to be "o#{webspace.data.webspaceId}"
end

mixin 'wait for notification' do |transaction_id|
  assert transaction_id.not_to be_empty

  assert 'receiving notification' do
    http 'morepork' do
      get "#{env.tenant}/eventing/notifications/history?transactionId=#{transaction_id}"
      ensure_success!
    end

    report 'no notification received' if response.json.empty?
  end
end