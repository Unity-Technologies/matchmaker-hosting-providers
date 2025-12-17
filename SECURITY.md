# Security Policy

## Reporting Security Vulnerabilities

If you discover a security vulnerability in this project, please report it responsibly:

**DO NOT** open a public issue. Instead, please report security vulnerabilities to:

- Unity Security Team: [security@unity3d.com](mailto:security@unity3d.com)

Please include the following information in your report:
- Description of the vulnerability
- Steps to reproduce the issue
- Potential impact
- Any suggested fixes (optional)

We will acknowledge receipt of your vulnerability report and send you regular updates about our progress.

## Security Best Practices

### Secrets and Credentials

**Never commit secrets, credentials, or sensitive information to this repository.**

- Use environment variables for all sensitive configuration
- Store secrets in Unity Dashboard under `Administration -> Secrets`
- Use the provided `.env.template` file as a guide for required environment variables
- Ensure your `.gitignore` file includes patterns for environment files and credentials

### AWS/Cloud Provider Credentials

- Never hardcode AWS access keys, API keys, or other provider credentials
- Use IAM roles and least-privilege principles when configuring access
- Rotate credentials regularly
- Follow each provider's security best practices:
  - [AWS Security Best Practices](https://aws.amazon.com/security/best-practices/)
  - [Microsoft Azure Security Best Practices](https://docs.microsoft.com/en-us/azure/security/fundamentals/best-practices-and-patterns)
  - [Unity Cloud Security](https://unity.com/legal/security)

### Dependencies

- Keep dependencies up to date
- Review security advisories for the packages you use
- Use Dependabot or similar tools to monitor for vulnerable dependencies

## Security Scanning

Before making any commits:
1. Run a secret scan to ensure no credentials are being committed
2. Review all changes to ensure no sensitive data is included
3. Validate that environment variables are properly referenced, not hardcoded

## Supported Versions

This project provides example integrations and is not officially supported. Security updates will be made on a best-effort basis.

## Disclaimer

This project is provided "as-is" without warranty. Users are responsible for:
- Securing their own deployments
- Managing their credentials and access controls
- Complying with all applicable security policies and regulations
- Following security best practices for their chosen hosting providers
