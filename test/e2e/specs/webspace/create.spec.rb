describe 'morepork' do
  it 'does create a new webspace', tags: [:webspace, :create] do
    log 'doing some http request'

    stack_id = 1234567

    http 'morepork' do
      post "#{env.tenant}/stack-instances/#{stack_id}/webspaces"

      json({
        "data": {
          "platform": "linux"
        }
      })
    end

    transaction_id = response.headers['Transaction-Id']

    assert transaction_id.not_to be_empty

    assert response.code.to be 202

    also 'check webspace properties', with: response.json

    system_id = response.json.systemInstanceId

    http 'morepork' do
      get "#{env.tenant}/stack-instances/#{stack_id}/webspaces/#{system_id}"
    end

    assert response.code.to be 200

    also 'check webspace properties', with: response.json
  end
end
