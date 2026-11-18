namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>
/// O retrato de uma pessoa do cadastro de assinantes no instante da conferência. O nome é público no
/// documento e só é lido na emissão; CPF, e-mail e telefone nunca chegam aqui.
/// </summary>
/// <param name="Codigo">O código opaco e estável da pessoa, que a lista da fase cita.</param>
/// <param name="Nome">O nome que o documento emitido mostra.</param>
/// <param name="UnidadeId">A unidade a que a pessoa está vinculada.</param>
/// <param name="Ativa">Se o cadastro da pessoa está ativo.</param>
/// <param name="AceiteVigente">Se o aceite do papel de assinante está em vigor.</param>
internal sealed record PessoaDoCadastroDeAssinantes(
    string Codigo, string Nome, Guid UnidadeId, bool Ativa, bool AceiteVigente);
