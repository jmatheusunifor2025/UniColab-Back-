using System.Text.RegularExpressions;
using UniColab.Api.Dtos;
using UniColab.Api.Entities;
using UniColab.Api.Repositories;
using UniColab.Api.Seguranca;

namespace UniColab.Api.Services;

public interface IAuthServico
{
    Task<LoginResposta> EntrarAsync(LoginRequest pedido, CancellationToken ct);
    Task<LoginResposta> RegistrarAsync(CadastroRequest pedido, CancellationToken ct);
    Task<List<CursoDto>> CursosPublicosAsync(CancellationToken ct);
    Task<UsuarioResumo> EuAsync(CancellationToken ct);
}

public sealed class AuthServico : IAuthServico
{
    private readonly IUsuarioRepositorio _usuarios;
    private readonly IUnidadeDeTrabalho _unidade;
    private readonly IUsuarioAtual _atual;
    private readonly JwtEmissor _jwt;

    public AuthServico(IUsuarioRepositorio usuarios, IUnidadeDeTrabalho unidade, IUsuarioAtual atual, JwtEmissor jwt)
    {
        _usuarios = usuarios;
        _unidade = unidade;
        _atual = atual;
        _jwt = jwt;
    }

    public async Task<LoginResposta> EntrarAsync(LoginRequest pedido, CancellationToken ct)
    {
        var identificador = pedido.Identificador.Trim();
        var encontrados = await _usuarios.BuscarParaLoginAsync(identificador, pedido.SiglaInstituicao?.Trim(), ct);
        if (encontrados.Count > 1)
        {
            throw new RegraNegocioException("Informe a sigla da instituição. Essa matrícula aparece em mais de uma.");
        }

        var usuario = encontrados.SingleOrDefault();
        if (usuario is null || !usuario.Ativo || !SenhaHasher.Confere(pedido.Senha, usuario.SenhaHash))
        {
            throw new RegraNegocioException("Credenciais inválidas.");
        }

        usuario.UltimoAcessoEm = DateTime.Now;
        await _unidade.SalvarAsync(ct);
        return Resposta(usuario);
    }

    private LoginResposta Resposta(Entities.Usuario usuario)
    {
        var (token, expiraEm) = _jwt.Emitir(usuario);
        return new LoginResposta
        {
            Token = token,
            ExpiraEm = expiraEm,
            Usuario = Mapear(usuario)
        };
    }

    public async Task<LoginResposta> RegistrarAsync(CadastroRequest pedido, CancellationToken ct)
    {
        var email = pedido.Email.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(email, @"^[^@\s]+@edu\.unifor\.br$"))
        {
            throw new RegraNegocioException("Use um e-mail @edu.unifor.br.");
        }

        var papel = pedido.TipoPerfil.Trim().ToLowerInvariant();
        if (papel is not (Papeis.Estudante or Papeis.Professor))
        {
            throw new RegraNegocioException("O cadastro público cria conta de estudante ou professor.");
        }

        var nome = pedido.Nome.Trim();
        if (nome.Length < 3)
        {
            throw new RegraNegocioException("Informe o nome completo.");
        }

        var matricula = pedido.Matricula.Trim();
        if (!Regex.IsMatch(matricula, @"^[A-Za-z0-9.-]+$"))
        {
            throw new RegraNegocioException("A matrícula usa letras, números, ponto ou hífen.");
        }

        if (await _usuarios.EmailExisteAsync(email, ct))
        {
            throw new RegraNegocioException("E-mail já cadastrado.");
        }

        var instituicao = await _usuarios.ObterInstituicaoPorSiglaAsync("UNIFOR", ct)
            ?? throw new RegraNegocioException("A instituição UNIFOR não está disponível.");

        if (await _usuarios.MatriculaExisteAsync(instituicao.Id, matricula, ct))
        {
            throw new RegraNegocioException("Matrícula já cadastrada.");
        }

        long? cursoId = null;
        if (papel == Papeis.Estudante)
        {
            if (pedido.CursoId is not long escolhido)
            {
                throw new RegraNegocioException("Estudante precisa de um curso.");
            }

            _ = await _usuarios.ObterCursoAsync(instituicao.Id, escolhido, ct)
                ?? throw new RegraNegocioException("Curso não pertence à instituição.");
            cursoId = escolhido;
        }
        else if (pedido.CursoId is long cursoProfessor)
        {
            _ = await _usuarios.ObterCursoAsync(instituicao.Id, cursoProfessor, ct)
                ?? throw new RegraNegocioException("Curso não pertence à instituição.");
        }

        var usuario = new Usuario
        {
            InstituicaoId = instituicao.Id,
            CursoId = cursoId,
            Nome = nome,
            Email = email,
            SenhaHash = SenhaHasher.Gerar(pedido.Senha),
            TipoPerfil = papel,
            Matricula = matricula,
            ElegivelEnade = papel == Papeis.Estudante,
            Ativo = true
        };

        await using var tx = await _unidade.IniciarTransacaoAsync(ct);
        await _usuarios.AdicionarAsync(usuario, ct);
        await _unidade.SalvarAsync(ct);

        if (papel == Papeis.Professor)
        {
            var cursos = await _usuarios.ListarCursosAsync(instituicao.Id, ct);
            if (pedido.CursoId is long apenas)
            {
                cursos = cursos.Where(curso => curso.Id == apenas).ToList();
            }

            if (cursos.Count == 0)
            {
                throw new RegraNegocioException("Nenhum curso disponível para o professor.");
            }

            foreach (var curso in cursos)
            {
                _usuarios.AdicionarVinculo(new DocenteCurso
                {
                    UsuarioId = usuario.Id,
                    CursoId = curso.Id,
                    InstituicaoId = instituicao.Id
                });
            }

            await _unidade.SalvarAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Resposta(usuario);
    }

    public async Task<List<CursoDto>> CursosPublicosAsync(CancellationToken ct)
    {
        var instituicao = await _usuarios.ObterInstituicaoPorSiglaAsync("UNIFOR", ct)
            ?? throw new RegraNegocioException("A instituição UNIFOR não está disponível.");
        var cursos = await _usuarios.ListarCursosAsync(instituicao.Id, ct);
        return cursos.Select(curso => new CursoDto { Id = curso.Id, Nome = curso.Nome, Codigo = curso.Codigo }).ToList();
    }

    public async Task<UsuarioResumo> EuAsync(CancellationToken ct)
    {
        var usuario = await _usuarios.ObterAsync(_atual.InstituicaoId, _atual.Id, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");
        return Mapear(usuario);
    }

    public static UsuarioResumo Mapear(Entities.Usuario usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Nome,
        Email = usuario.Email,
        TipoPerfil = usuario.TipoPerfil,
        InstituicaoId = usuario.InstituicaoId,
        CursoId = usuario.CursoId,
        Matricula = usuario.Matricula
    };
}
