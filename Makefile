
.PHONY: help install_dotnet9 install_aspire setup_dev

help:
	@echo "Available targets:"
	@echo "  install_dotnet9  - Install .NET 9 SDK (required for Aspire)"
	@echo "  install_aspire   - Install .NET 9 and Aspire CLI"
	@echo "  setup_dev        - Complete development environment setup"
	@echo "  test             - Run all tests"
	@echo "  test-coverage    - Run tests with code coverage and display summary"
	@echo "  coverage-report  - Open the coverage HTML report in browser"
	@echo "  clean            - Clean build artifacts"
	@echo "  run              - Run Aspire application"
	@echo "  help             - Show this help message"

install_dotnet9_win:
	winget install --id Microsoft.DotNet.SDK.9 -e   

install_dotnet9_linux:
	@echo "Installing .NET 9 SDK..."
	@echo "Downloading Microsoft package source..."
	wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
	sudo dpkg -i packages-microsoft-prod.deb
	rm packages-microsoft-prod.deb
	@echo "Installing .NET 9 via Microsoft installation script..."
	curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0
	@echo "Adding .NET 9 to PATH in current session..."
	@echo "To make PATH change permanent, add this to your shell profile:"
	@echo 'export PATH="$$HOME/.dotnet:$$PATH"'
	@echo ""
	@echo "Verifying installation..."
	export PATH="$$HOME/.dotnet:$$PATH" && dotnet --version

install_dotnet9:
	# For Windows, uncomment the following line
	# make install_dotnet9_win
	# For Linux, uncomment the following line
	make install_dotnet9_linux


install_aspire: install_dotnet9
	@echo "Installing Aspire CLI tool..."
	export PATH="$$HOME/.dotnet:$$PATH" && dotnet tool install --global aspire.cli --prerelease

install_aspirate: install_dotnet9
	@echo "Installing Aspirate tool..."
	dotnet tool install --global aspirate --version 9.1.0

setup_dev: install_aspire
	@echo "Setting up development environment..."
	@echo "Restoring .NET workloads..."
	export PATH="$$HOME/.dotnet:$$PATH" && dotnet workload restore webhooks.sln
	@echo ""
	@echo "Development environment setup complete!"
	@echo "Remember to add this to your shell profile for permanent PATH update:"
	@echo 'export PATH="$$HOME/.dotnet:$$PATH"'


clean:
	rm */bin -rf || true
	rm */obj -rf || true

run:
	@echo "Running Aspire CLI..."
	aspire run

run_clean: clean run

test:
	@echo "Running tests..."
	 dotnet test --verbosity normal

test-coverage:
	@echo "Running tests with coverage..."
	 dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
	@echo ""
	@echo "Generating coverage report..."
	 reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:"Html;TextSummary"
	@echo ""
	@echo "========================================"
	@echo "CODE COVERAGE SUMMARY"
	@echo "========================================"
	@ grep -E "Line coverage:|Branch coverage:|Method coverage:" TestResults/CoverageReport/Summary.txt || type TestResults\CoverageReport\Summary.txt | findstr /C:"Line coverage:" /C:"Branch coverage:" /C:"Method coverage:"
	@echo "========================================"
	@echo "Full report: TestResults/CoverageReport/index.html"

coverage-report:
	@echo "Opening coverage report..."
	@start TestResults/CoverageReport/index.html || open TestResults/CoverageReport/index.html || xdg-open TestResults/CoverageReport/index.html

get_webhooks:
	curl https://localhost:7579/api/webhooks -k -v
