namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Conta as chamadas que os repositórios de Unidade e de Instituição receberam no host de teste: o que prova que
/// uma recusa veio antes de qualquer leitura do cadastro.
/// </summary>
public sealed class ConsultasAoCadastroContadas
{
    private int _total;

    public int Total => _total;

    public void Registrar() => Interlocked.Increment(ref _total);
}
