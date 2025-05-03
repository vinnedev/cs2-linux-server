#!/bin/bash

# Atualizar os pacotes
echo "Atualizando pacotes..."
sudo apt-get update -y

# Instalar dependências necessárias
echo "Instalando dependências..."
sudo apt-get install -y wget apt-transport-https

# Adicionar a chave GPG da Microsoft
echo "Adicionando chave GPG da Microsoft..."
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Atualizar os pacotes novamente
echo "Atualizando pacotes novamente..."
sudo apt-get update -y

# Instalar o runtime do .NET 8.0
echo "Instalando o runtime do .NET 8.0..."
sudo apt-get install -y dotnet-runtime-8.0

# Verificar a instalação
echo "Verificando a instalação..."
dotnet --info

echo "Instalação concluída!"