describe 'morepork' do
  it 'does create, read, update and delete a shared webspace', tags: [:webspace, :create, :read, :update, :delete] do
    stack_id = 1234567
    system_id = nil

    group 'create webspace' do
  
      http 'morepork' do
        post "#{env.tenant}/stack-instances/#{stack_id}/webspaces"

        json({
          "data": {
            "platform": "linux"
          }
        })
      end

      assert response.code.to be 202

      system_id = response.json.systemInstanceId

      also 'check webspace properties', with: response.json
      also 'wait for notification', with: response.headers['Transaction-Id']
    
    end
  
    group 'read webspace' do

      http 'morepork' do
        get "#{env.tenant}/stack-instances/#{stack_id}/webspaces/#{system_id}"
      end

      assert response.code.to be 200

      also 'check webspace properties', with: response.json

    end

    group 'update webspace' do

      webspace = response.json

      webspace.domains = [
        {
          "domain": "test-#{uuid 5}.example.com",
          "environment": "php8",
        }
      ]

      webspace.accounts = [
        {
          "password": "test-#{uuid}",
        }
      ]

      http 'morepork' do
        put "#{env.tenant}/stack-instances/#{stack_id}/webspaces/#{system_id}"
        json(webspace)
      end

      assert response.code.to be 202

      also 'check webspace properties', with: response.json
      also 'wait for notification', with: response.headers['Transaction-Id']

    end

    group 'delete webspace' do

      http 'morepork' do
        delete "#{env.tenant}/stack-instances/#{stack_id}/webspaces/#{system_id}"
      end

      assert response.code.to be 202

      also 'wait for notification', with: response.headers['Transaction-Id']

    end
  end
end
