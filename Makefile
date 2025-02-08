local_setup:
	# install dotnet sdk 9.0
	wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
	sudo dpkg -i packages-microsoft-prod.deb
	sudo apt-get update
	sudo apt-get install -y apt-transport-https
	sudo apt-get update
	sudo apt-get install -y dotnet-sdk-9.0
	# install aspirate
	dotnet tool install -g aspirate

get_webhooks:
	curl https://localhost:7579/api/webhooks -k -v
