# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in Valir, please report it responsibly:

1. **Do NOT** open a public GitHub issue
2. Email security concerns to: [taiizor@vegalya.com]
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes (optional)

## Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial Assessment**: Within 1 week
- **Fix Timeline**: Depends on severity
  - Critical: 24-72 hours
  - High: 1-2 weeks
  - Medium: 1 month
  - Low: Next release

## Security Best Practices

When using Valir:

1. **Redis Security**
   - Use TLS for Redis connections in production
   - Configure Redis AUTH password
   - Use network isolation (VPC, firewall rules)

2. **Payload Security**
   - Encrypt sensitive data before storing in job payloads
   - Never store credentials in job payloads

3. **Event Bus Security**
   - Use TLS for broker connections
   - Configure authentication for Kafka/RabbitMQ/Azure SB

4. **Dependency Updates**
   - Keep Valir and its dependencies updated
   - Monitor for security advisories

## Acknowledgments

We appreciate security researchers who help keep Valir secure. Contributors who report valid vulnerabilities will be acknowledged (unless they prefer anonymity).
